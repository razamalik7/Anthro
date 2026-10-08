namespace Anthro.Sim.People
{
    /// <summary>
    /// How well a band keeps out the cold. Fire and windbreaks are known from the start;
    /// caves, hide clothing and tents raise protection as they're discovered.
    /// </summary>
    public static class Shelter
    {
        /// <summary>How well someone is kept from the cold: a burning fire to sit at (their family's, or kin's or a friend's) plus what the band knows.</summary>
        public static double Protection(Band band, Person person)
        {
            double p = 0;
            if (person.Home != null && person.Home.FireLit) p += 0.3;
            // With their own fire out, people sit at the hearth of kin or a close friend.
            else if (System.Linq.Enumerable.Any(band.Households, h => h.FireLit && System.Linq.Enumerable.Any(h.Members, m => m.IsKin(person) || person.GetAffinity(m) > 0.3))) p += 0.25;
            if (band.AnyoneKnows("shelter.brush")) p += 0.2;
            if (band.AnyoneKnows("hide.wrap")) p += 0.15;
            if (band.AnyoneKnows("shelter.cave")) p += 0.15;
            if (band.AnyoneKnows("hide.tent")) p += 0.15;
            if (band.AnyoneKnows("hide.needle")) p += 0.2;
            if (band.AnyoneKnows("hide.scrape")) p += 0.05;      // softened hides make better wraps
            if (band.AnyoneKnows("hide.awl")) p += 0.05;         // and sewn ones better still
            if (band.AnyoneKnows("bone.awl")) p += 0.03;         // holes punched for lacing
            if (band.AnyoneKnows("bone.needle")) p += 0.05;      // close-fitting seams
            if (band.AnyoneKnows("shelter.lean")) p += 0.1;
            if (band.AnyoneKnows("shelter.hut")) p += 0.15;
            if (band.AnyoneKnows("shelter.pit")) p += 0.2;
            if (band.AnyoneKnows("fire.hearth")) p += 0.05;      // a built hearth holds its heat
            if (band.AnyoneKnows("hide.scrape")) p += 0.05;      // softened hides make better wraps
            if (band.AnyoneKnows("hide.awl")) p += 0.05;         // and sewn ones better still
            if (band.AnyoneKnows("shelter.lean")) p += 0.1;
            if (band.AnyoneKnows("shelter.hut")) p += 0.15;
            if (band.AnyoneKnows("shelter.pit")) p += 0.2;
            if (band.AnyoneKnows("fire.hearth")) p += 0.05;      // a built hearth holds its heat
            return p;
        }

        /// <summary>Below this temperature, people start losing health to cold.</summary>
        public static double ComfortTemp(Band band, Person person) => 5 - Protection(band, person) * 15;
    }
}
