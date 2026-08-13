namespace WordPuzzle.Data
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        LevelComplete,
        GameOver
    }

    public enum HintType
    {
        SingleTile,
        TargetTile,
        ShuffleWheel
    }

    public enum WordOrientation
    {
        Horizontal,
        Vertical
    }

    public enum UIPresetOrientation
    {
        Portrait,
        Landscape
    }
}
