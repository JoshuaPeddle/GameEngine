namespace GameEngine.Core.Utils
{
    public class FrameSmoother
    {
        private readonly Queue<double> _deltaTimes;
        private readonly int _sampleSize;
        private double _previousTime;

        public FrameSmoother(int sampleSize = 10)
        {
            _sampleSize = sampleSize;
            _deltaTimes = new Queue<double>(_sampleSize);
            _previousTime = GetCurrentTime();
        }

        public double GetSmoothedDelta()
        {
            double currentTime = GetCurrentTime();
            double frameDelta = currentTime - _previousTime;
            _previousTime = currentTime;

            _deltaTimes.Enqueue(frameDelta);

            if (_deltaTimes.Count > _sampleSize)
                _deltaTimes.Dequeue();

            return _deltaTimes.Average() * 1000;
        }

        // Replace with actual timing source
        private double GetCurrentTime()
        {
            return (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
