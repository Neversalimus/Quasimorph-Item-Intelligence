namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static bool _test3RowsRefreshPending;
        private static int _test3RowsRefreshDelayFrames;

        private static void QueueTest3RowsRefresh()
        {
            if (!_inspectorOpen || _inspectorRoot == null) return;
            _test3RowsRefreshPending = true;
            if (_test3RowsRefreshDelayFrames < 1) _test3RowsRefreshDelayFrames = 1;
        }

        private static void TickTest3RowsRefresh()
        {
            if (!_test3RowsRefreshPending) return;
            if (!_inspectorOpen || _inspectorRoot == null)
            {
                _test3RowsRefreshPending = false;
                _test3RowsRefreshDelayFrames = 0;
                return;
            }

            if (_test3RowsRefreshDelayFrames > 0)
            {
                _test3RowsRefreshDelayFrames--;
                return;
            }

            _test3RowsRefreshPending = false;
            RenderBrowserRowsOnly();
        }
    }
}