"""Cross-check independent custom audio recordings and optional Editor/IL2CPP equivalence."""
import argparse
import json
import math
from pathlib import Path

from analyze_custom_spatial_audio import parse_float_wave


def verify(directory: Path, reference: Path | None = None) -> dict:
    report = json.loads((directory / "report.json").read_text(encoding="utf-8-sig"))
    assert report["passed"] and all(case["passed"] for case in report["cases"])
    recordings = {case["name"]: parse_float_wave((directory / (case["name"] + ".wav")).read_bytes())
                  for case in report["cases"]}
    checks = {}

    def data(name):
        return recordings[name][1]

    direct, early, late, mixed = (data("small-room-" + part) for part in ("direct", "early", "late", "mixed"))
    assert len(direct) == len(early) == len(late) == len(mixed)
    error = max(abs(d + e + l - m) for d, e, l, m in zip(direct, early, late, mixed))
    assert error < 1e-6, error
    checks["component_sum_max_error"] = error
    assert data("open-boundaries-mixed") == direct
    checks["open_boundaries_equal_direct"] = True

    opened, quarter, closed = (data("l-corridor-" + state) for state in ("open", "quarter-open", "closed"))
    assert len(opened) == len(quarter) == len(closed)
    assert max(map(abs, closed)) == 0
    error = max(abs(q - .5 * o) for o, q in zip(opened, quarter))
    assert error < 1e-7
    # Independent geometry: source(1,1.5,1), portal(7,1.5,2), listener(7,1.5,9).
    expected = (math.sqrt(37) + 7) / 343 * recordings["l-corridor-open"][0]
    onset = next(i // 2 for i, value in enumerate(opened) if abs(value) > 1e-8)
    assert abs(onset - expected) <= 1, (onset, expected)
    assert all(abs(opened[i] - opened[i + 1]) < 1e-7 for i in range(0, len(opened), 2))
    checks["portal"] = {"quarter_open_amplitude_max_error": error, "closed_silent": True,
                        "first_frame": onset, "expected_fractional_frame": expected,
                        "arrival_from_final_portal_not_through_wall": True}

    front, back, up, right = (data("binaural-" + direction) for direction in ("front", "back", "up", "right"))
    differences = {name: max(abs(a - b) for a, b in zip(front, other))
                   for name, other in (("front_back", back), ("front_up", up))}
    assert all(value > 1e-4 for value in differences.values())
    left_energy = sum(value * value for value in right[::2])
    right_energy = sum(value * value for value in right[1::2])
    assert right_energy > left_energy
    left_peak = max(range(len(right) // 2), key=lambda i: abs(right[2 * i]))
    right_peak = max(range(len(right) // 2), key=lambda i: abs(right[2 * i + 1]))
    assert right_peak < left_peak
    checks["binaural"] = {"directional_max_differences": differences,
                           "right_source_left_energy": left_energy, "right_source_right_energy": right_energy,
                           "right_source_left_peak_frame": left_peak, "right_source_right_peak_frame": right_peak,
                           "note": "Signal verification only; not a perceptual localization evaluation."}

    if reference:
        maximum = 0
        for name, (rate, samples) in recordings.items():
            other_rate, other = parse_float_wave((reference / (name + ".wav")).read_bytes())
            assert rate == other_rate and len(samples) == len(other), name
            maximum = max(maximum, max(abs(a - b) for a, b in zip(samples, other)))
        assert maximum < 1e-6, maximum
        checks["reference_max_sample_difference"] = maximum
    result = {"passed": True, "checks": checks}
    (directory / "signal-verification.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--reference", type=Path)
    args = parser.parse_args()
    print(json.dumps(verify(args.directory, args.reference), indent=2))
