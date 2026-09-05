namespace GameEngine.Core
{
    // Turns however long a host frame happened to take into a whole number of fixed simulation
    // steps, so the step sequence follows elapsed time rather than the host's frame schedule.
    //
    // Time beyond the catch-up budget is dropped rather than queued. A simulation that cannot
    // keep up has to fall behind the wall clock; queueing the shortfall would make the next
    // frame longer still, and the frame after that longer again.
    public sealed class FixedStepAccumulator
    {
        public FixedStepAccumulator(double stepSeconds, int maxStepsPerFrame)
        {
            if (!double.IsFinite(stepSeconds) || stepSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds,
                    "A simulation step must be a finite, positive number of seconds.");

            if (maxStepsPerFrame < 1)
                throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame), maxStepsPerFrame,
                    "A frame must be allowed at least one simulation step.");

            StepSeconds = stepSeconds;
            MaxStepsPerFrame = maxStepsPerFrame;
        }

        public double StepSeconds { get; }

        public int MaxStepsPerFrame { get; }

        /// <summary>Elapsed time carried into the next frame, always less than one step.</summary>
        public double Pending { get; private set; }

        /// <summary>Elapsed time discarded because it exceeded the catch-up budget.</summary>
        public double DroppedSeconds { get; private set; }

        public int Advance(double elapsedSeconds)
        {
            if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds,
                    "Elapsed time must be a finite, non-negative number of seconds.");

            Pending += elapsedSeconds;

            double available = Pending / StepSeconds;
            if (available >= MaxStepsPerFrame)
            {
                DroppedSeconds += Pending - MaxStepsPerFrame * StepSeconds;
                Pending = 0;
                return MaxStepsPerFrame;
            }

            int steps = (int)available;
            Pending -= steps * StepSeconds;
            return steps;
        }

        /// <summary>Drops the pending remainder. Used when the engine pauses or resumes.</summary>
        public void Reset() => Pending = 0;
    }
}
