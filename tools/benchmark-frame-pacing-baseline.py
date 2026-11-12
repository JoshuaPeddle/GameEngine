import argparse
import os
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument("--ref", default="833cddd")
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory(prefix="ge-pacing-baseline-") as temporary:
    target = Path(temporary).resolve()
    archive = subprocess.run(["git", "archive", args.ref], cwd=root, check=True, capture_output=True)
    subprocess.run(["tar", "-xf", "-", "-C", str(target)], input=archive.stdout, check=True)
    benchmark = (root / "GameEngine.Benchmarks/FramePacingBenchmark.cs").read_text()
    benchmark = benchmark.replace("public override FrameActivity FrameActivity => activity;", "")
    benchmark = benchmark.replace("            FrameActivity = FrameActivity.Idle,\n            CacheBackground = true,\n", "")
    benchmark = benchmark.replace('        Measure("paced_frame", () => engine.Present(1.0 / 60));', "")
    benchmark += "\npublic enum FrameActivity { Idle, Ambient, Active }\n"
    (target / "GameEngine.Benchmarks/FramePacingBenchmark.cs").write_text(benchmark)
    program = target / "GameEngine.Benchmarks/Program.cs"
    program.write_text(program.read_text().replace('if (args.Contains("--effects"))', 'if (args.Contains("--frame-pacing"))\n    FramePacingBenchmark.Run();\nelse if (args.Contains("--effects"))', 1))
    subprocess.run(["dotnet", "run", "--project", "GameEngine.Benchmarks", "-c", "Release", "--", "--frame-pacing"], cwd=target, env=os.environ, check=True)
