namespace HorrorEngine
{
    // Visual-only lifecycle. PlayerStateReload remains responsible for ammo transfer.
    public interface IWeaponReloadPresentation
    {
        void BeginReload(float duration, float loaded, int roundsToLoad);
        void EndReload(bool completed);
    }
}
