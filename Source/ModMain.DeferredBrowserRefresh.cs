namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _browserRowsRefreshPending;
        private static int _browserRowsRefreshDelayFrames;

        private static void QueueBrowserRowsRefresh()
        {
            if (!_inspectorOpen || _inspectorRoot == null) return;
            _browserRowsRefreshPending = true;
            if (_browserRowsRefreshDelayFrames < 1) _browserRowsRefreshDelayFrames = 1;
        }

        private static void TickBrowserRowsRefresh()
        {
            if (!_browserRowsRefreshPending) return;
            if (!_inspectorOpen || _inspectorRoot == null)
            {
                _browserRowsRefreshPending = false;
                _browserRowsRefreshDelayFrames = 0;
                return;
            }

            if (_browserRowsRefreshDelayFrames > 0)
            {
                _browserRowsRefreshDelayFrames--;
                return;
            }

            _browserRowsRefreshPending = false;
            RenderBrowserRowsOnly();
        }
    }
}