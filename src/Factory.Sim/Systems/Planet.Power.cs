namespace Factory.Sim;

/// <summary>Electric grid: poles connect within wire reach; machines in a pole's supply area join its network.</summary>
public sealed partial class Planet
{
    /// <summary>Electric networks on this planet keyed by network id, rebuilt whenever poles or machines change.</summary>
    public SortedDictionary<int, PowerNetwork> Networks { get; } = new();

    private bool Covers(Building pole, Building b)
    {
        int r = pole.Def.SupplyRadius;
        foreach (Int2 t in b.Footprint())
        {
            if (Torus.Chebyshev(pole.Origin, t) <= r)
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildPowerNetworks()
    {
        Networks.Clear();
        List<Building> poles = Buildings.Where(b => b.Def.Kind == BuildingKind.Pole).ToList();
        int[] parent = new int[poles.Count];
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        int Find(int x)
        {
            while (parent[x] != x)
            {
                x = parent[x] = parent[parent[x]];
            }

            return x;
        }

        for (int i = 0; i < poles.Count; i++)
        {
            for (int j = i + 1; j < poles.Count; j++)
            {
                int reach = System.Math.Min(poles[i].Def.WireReach, poles[j].Def.WireReach);
                if (Torus.Chebyshev(poles[i].Origin, poles[j].Origin) <= reach)
                {
                    int a = Find(i), b = Find(j);
                    if (a != b)
                    {
                        parent[System.Math.Max(a, b)] = System.Math.Min(a, b);
                    }
                }
            }
        }

        for (int i = 0; i < poles.Count; i++)
        {
            int rootId = poles[Find(i)].Id;
            if (Networks.TryGetValue(rootId, out PowerNetwork? net) == false)
            {
                Networks[rootId] = net = new PowerNetwork { Id = rootId };
            }

            net.Poles.Add(poles[i]);
            poles[i].NetworkId = rootId;
        }

        foreach (Building b in Buildings)
        {
            if (b.Def.Kind == BuildingKind.Pole)
            {
                continue;
            }

            bool electric = (b.Def.Energy == EnergyKind.Electric) || (b.Def.Kind is BuildingKind.SteamEngine or BuildingKind.Battery);
            b.NetworkId = 0;
            if (electric == false)
            {
                continue;
            }

            for (int i = 0; i < poles.Count; i++)
            {
                if (Covers(poles[i], b) == false)
                {
                    continue;
                }

                PowerNetwork net = Networks[poles[Find(i)].Id];
                b.NetworkId = net.Id;
                if (b.Def.Kind == BuildingKind.SteamEngine)
                {
                    net.Generators.Add(b);
                }
                else if (b.Def.Kind == BuildingKind.Battery)
                {
                    net.Batteries.Add(b);
                }
                else
                {
                    net.Consumers.Add(b);
                }

                break;
            }
        }
    }

    private bool EngineCanRun(Building e)
    {
        if (IsAdjacentToWater(e.Origin, e.W, e.H) == false)
        {
            e.Status = MachineStatus.NoWater;

            return false;
        }

        if ((e.FuelJ > 0) || e.Inv.Items.Any(kv => World.Content.IsFuel(kv.Key)))
        {
            return true;
        }

        e.Status = MachineStatus.NoFuel;

        return false;
    }

    private void UpdatePower()
    {
        // Anything not on a network gets no power.
        foreach (Building b in Buildings)
        {
            if ((b.Def.Energy == EnergyKind.Electric) && (b.NetworkId == 0))
            {
                b.Satisfaction = 0;
            }

            if (b.Def.Kind == BuildingKind.SteamEngine)
            {
                b.LastOutputJ = 0;
            }
        }

        foreach (PowerNetwork net in Networks.Values)
        {
            long demand = 0;
            foreach (Building c in net.Consumers)
            {
                if (WantsPower(c))
                {
                    demand += PerTickJ(c.Def.PowerKw);
                }
            }

            long genCap = 0;
            List<Building> running = new List<Building>();
            foreach (Building g in net.Generators)
            {
                if (EngineCanRun(g))
                {
                    running.Add(g);
                    genCap += PerTickJ(g.Def.OutputKw);
                }
            }

            long dischargeCap = 0, chargeCap = 0, stored = 0, capacity = 0;
            foreach (Building bt in net.Batteries)
            {
                long cap = bt.Def.CapacityKj * 1000L, rate = PerTickJ(bt.Def.RateKw);
                dischargeCap += System.Math.Min(rate, bt.StoredJ);
                chargeCap += System.Math.Min(rate, cap - bt.StoredJ);
            }

            long fromGen = System.Math.Min(demand, genCap);
            long discharge = System.Math.Min(demand - fromGen, dischargeCap);
            long chargeIn = System.Math.Min(genCap - fromGen, chargeCap);
            int sat = demand == 0 ? 1000 : (int)((fromGen + discharge) * 1000 / demand);

            // Generators: fill in id order, burning fuel at their (poor) efficiency.
            long remaining = fromGen + chargeIn;
            foreach (Building g in running)
            {
                long produce = System.Math.Min(remaining, PerTickJ(g.Def.OutputKw));
                if (produce <= 0)
                {
                    g.Status = MachineStatus.Idle;
                    continue;
                }

                long burn = produce * 100 / System.Math.Max(1, g.Def.Efficiency);
                BurnerHasEnergy(g, burn);
                if (g.FuelJ < burn)
                {
                    produce = g.FuelJ * g.Def.Efficiency / 100;
                    burn = g.FuelJ;
                }

                g.FuelJ -= burn;
                g.LastOutputJ = produce;
                g.Status = MachineStatus.Working;
                remaining -= produce;
            }

            // Batteries: discharge to cover deficit, then store surplus (lossy).
            long toDischarge = discharge;
            long toCharge = chargeIn * 100;
            foreach (Building bt in net.Batteries)
            {
                long rate = PerTickJ(bt.Def.RateKw), cap = bt.Def.CapacityKj * 1000L;
                long d = System.Math.Min(toDischarge, System.Math.Min(rate, bt.StoredJ));
                bt.StoredJ -= d;
                toDischarge -= d;
                long cIn = System.Math.Min(toCharge / 100, System.Math.Min(rate, cap - bt.StoredJ));
                toCharge -= cIn * 100;
                bt.StoredJ += cIn * bt.Def.Efficiency / 100;
                stored += bt.StoredJ;
                capacity += cap;
                bt.Status = (d > 0) || (cIn > 0) ? MachineStatus.Working : MachineStatus.Idle;
            }

            foreach (Building c in net.Consumers)
            {
                c.Satisfaction = sat;
            }

            net.DemandJ = demand;
            net.SuppliedJ = fromGen + discharge;
            net.GenCapacityJ = genCap;
            net.StoredJ = stored;
            net.StorageCapacityJ = capacity;
            net.Satisfaction = sat;
        }
    }
}
