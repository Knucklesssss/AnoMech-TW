namespace AnoMech.Scenarios.Top;

// How much of the party gets a sign during Hello World.
//
// System marks everyone, including the debuff holders, so the whole assignment is
// readable at a glance while learning. Manual marks only what a raid actually places
// by hand: the debuff holders are left blank because they already know from their
// own debuff, and a real party would not waste a sign on them.
public enum MarkerMode
{
    System,
    Manual,
}
