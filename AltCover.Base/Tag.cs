namespace AltCover.Recorder
{
#if DEBUG  // conditionally internal for Gendarme

    public
#else
    internal
#endif
        enum Tag
    {
        Null = 0,
        Time = 1,
        Call = 2,
        Both = 3
    }
}