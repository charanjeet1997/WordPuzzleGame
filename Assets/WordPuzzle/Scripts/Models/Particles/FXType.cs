namespace WordPuzzle.Particles
{
    /// <summary>
    /// Indexes into the ParticleFX FactoryConfig prefab list. The numeric values are the
    /// factory indices, so the order here must match the prefab order in the config.
    /// </summary>
    public enum FXType
    {
        TileReveal = 0,
        WordMatchBurst = 1,
        BonusWordSparkle = 2,
        LevelCompleteFireworks = 3
    }
}
