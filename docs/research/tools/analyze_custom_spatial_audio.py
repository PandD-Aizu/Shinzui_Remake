#!/usr/bin/env python3
"""Analyze the Phase A custom room probe's IEEE-float stereo impulse WAVs.

Python standard library only. This is a reproducible diagnostic, not an ISO room
measurement or a reproduction of a cited paper. Broad biquad bandpass estimates
describe the filtered output spectrum; the DSP's overlapping crossover branches
have independent RT60 targets and are not those same measurement bands.

Usage:
    python docs/research/tools/analyze_custom_spatial_audio.py --self-test
    python docs/research/tools/analyze_custom_spatial_audio.py
    python docs/research/tools/analyze_custom_spatial_audio.py Artifacts/CustomSpatialAudio/Windows
"""

from __future__ import annotations

import argparse
from array import array
import csv
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import struct
import sys
from typing import Any


BAND_CENTRES_HZ = (125, 1000, 8000)
BAND_Q = 0.707
MINIMUM_TERMINAL_POWER_DROP_DB = 35.0
MAXIMUM_OMITTED_ENERGY_FRACTION = 0.05
DEFAULT_DIRECTORY = Path(__file__).resolve().parents[3] / "Artifacts/CustomSpatialAudio/Editor"
METHOD_NOTE = (
    "Filtered stereo spectrum estimates: independent constant-peak biquad bandpasses "
    "at 125/1000/8000 Hz, Q=0.707; squared channel energies are summed, not waveforms. "
    "Backward Schroeder EDC; linear regression from -5 to -25 dB gives T20 and an "
    "extrapolated RT60=T20*3. This is not a measured 60 dB decay, an ISO-compliant "
    "measurement, or exact reproduction of the cited FDN paper. The separate low/mid/high "
    "FDN component targets do not equal these broad spectral measurement bands."
)


def parse_float_wave(raw: bytes) -> tuple[int, array]:
    """Read the probe's RIFF format 3, stereo, 32-bit IEEE-float WAV exactly."""
    if len(raw) < 12 or raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        raise ValueError("Expected little-endian RIFF/WAVE.")
    declared_end = struct.unpack_from("<I", raw, 4)[0] + 8
    if declared_end != len(raw):
        raise ValueError("RIFF length does not match the captured file length.")
    offset = 12
    wave_format = None
    payload = None
    while offset < declared_end:
        if offset + 8 > declared_end:
            raise ValueError("Truncated RIFF chunk header.")
        chunk_id = raw[offset:offset + 4]
        chunk_size = struct.unpack_from("<I", raw, offset + 4)[0]
        start, end = offset + 8, offset + 8 + chunk_size
        if end > declared_end:
            raise ValueError("Truncated RIFF chunk payload.")
        if chunk_id == b"fmt ":
            if wave_format is not None or chunk_size < 16:
                raise ValueError("Invalid or repeated format chunk.")
            wave_format = struct.unpack_from("<HHIIHH", raw, start)
        elif chunk_id == b"data":
            if payload is not None:
                raise ValueError("Repeated data chunks are unsupported.")
            payload = raw[start:end]
        offset = end + (chunk_size & 1)
    if offset != declared_end or wave_format is None or payload is None:
        raise ValueError("Missing WAV format/data or invalid chunk padding.")
    format_tag, channels, rate, byte_rate, block_align, bits = wave_format
    if (format_tag, channels, bits, block_align) != (3, 2, 32, 8):
        raise ValueError("Expected format 3 IEEE-float stereo, 32 bits per sample.")
    if rate <= 0 or byte_rate != rate * block_align or len(payload) % block_align:
        raise ValueError("Invalid sample rate, byte rate, or partial stereo frame.")
    samples = array("f")
    samples.frombytes(payload)
    if sys.byteorder != "little":
        samples.byteswap()
    if not all(math.isfinite(value) for value in samples):
        raise ValueError("WAV contains non-finite samples; decay analysis is invalid.")
    return rate, samples


def band_energy(samples: array, rate: int, centre: float) -> array:
    """Constant-peak RBJ-style bandpass, transposed direct form II, stereo energy."""
    if not 0 < centre < rate / 2:
        raise ValueError("Band centre must be below Nyquist.")
    omega = 2.0 * math.pi * centre / rate
    alpha = math.sin(omega) / (2.0 * BAND_Q)
    denominator = 1.0 + alpha
    b0 = alpha / denominator
    b2 = -b0
    a1 = -2.0 * math.cos(omega) / denominator
    a2 = (1.0 - alpha) / denominator
    left_z1 = left_z2 = right_z1 = right_z2 = 0.0
    energy = array("d")
    for offset in range(0, len(samples), 2):
        left_in, right_in = samples[offset], samples[offset + 1]
        left = b0 * left_in + left_z1
        right = b0 * right_in + right_z1
        left_z1, left_z2 = -a1 * left + left_z2, b2 * left_in - a2 * left
        right_z1, right_z2 = -a1 * right + right_z2, b2 * right_in - a2 * right
        # Flush negligible filter state, far below the fit interval, to avoid denormals.
        if abs(left_z1) < 1e-30:
            left_z1 = 0.0
        if abs(left_z2) < 1e-30:
            left_z2 = 0.0
        if abs(right_z1) < 1e-30:
            right_z1 = 0.0
        if abs(right_z2) < 1e-30:
            right_z2 = 0.0
        energy.append(left * left + right * right)
    return energy


def fit_decay(energy: array, rate: int) -> dict[str, Any]:
    """Fit T20 with conservative finite-capture checks; never infer support from final EDC zero."""
    result: dict[str, Any] = {"accepted": False, "reason": None}
    frames = len(energy)
    if not frames or not any(energy):
        result["reason"] = "No measurable band energy."
        return result
    cumulative = array("d", [0.0]) * frames
    remaining = 0.0
    for index in range(frames - 1, -1, -1):
        remaining += energy[index]
        cumulative[index] = remaining
    total = cumulative[0]
    result["captured_band_energy"] = total
    start = next((i for i, value in enumerate(cumulative) if value <= total * 10 ** (-5 / 10)), None)
    end = next((i for i, value in enumerate(cumulative) if value <= total * 10 ** (-25 / 10)), None)
    if start is None or end is None or end <= start:
        result["reason"] = "Captured EDC does not support distinct -5 and -25 dB crossings."
        return result

    # At most 1 kHz regression sampling avoids overweighting high sample rates.
    stride = max(1, rate // 1000)
    indices = list(range(start, end + 1, stride))
    if indices[-1] != end:
        indices.append(end)
    points = [(i / rate, 10.0 * math.log10(cumulative[i] / total))
              for i in indices if cumulative[i] > 0]
    if len(points) < 10:
        result["reason"] = "Fewer than 10 nonzero EDC fit points; impulse/filter response is too short."
        return result
    mean_x = sum(x for x, _ in points) / len(points)
    mean_y = sum(y for _, y in points) / len(points)
    variance_x = sum((x - mean_x) ** 2 for x, _ in points)
    slope = sum((x - mean_x) * (y - mean_y) for x, y in points) / variance_x
    intercept = mean_y - slope * mean_x
    variance_y = sum((y - mean_y) ** 2 for _, y in points)
    residual = sum((y - (slope * x + intercept)) ** 2 for x, y in points)
    r_squared = 1.0 - residual / variance_y if variance_y > 0 else 0.0
    if not slope < 0:
        result["reason"] = "Nonnegative decay slope."
        return result
    rt60 = -60.0 / slope
    fit_span = (end - start) / rate
    guard_frames = max(1, min(frames, round(rate * 0.1)))
    terminal_power = sum(energy[-guard_frames:]) / guard_frames
    bin_frames = max(1, round(rate * 0.01))
    peak_window_power = max(sum(energy[i:i + bin_frames]) / len(energy[i:i + bin_frames])
                            for i in range(0, frames, bin_frames))
    terminal_drop = (10.0 * math.log10(peak_window_power / terminal_power)
                     if terminal_power > 0 else None)
    # For exponential energy decay, omitted integral ~= terminal power*T60/(6 ln10).
    # This is only a truncation diagnostic; no fitted curve is corrected with extrapolated energy.
    omitted_estimate = terminal_power * rate * rt60 / (6.0 * math.log(10.0))
    omitted_fraction = omitted_estimate / cumulative[end] if cumulative[end] > 0 else None
    time_after_fit = (frames - 1 - end) / rate
    result.update({
        "fit_start_seconds": start / rate,
        "fit_end_seconds": end / rate,
        "fit_start_db": 10.0 * math.log10(cumulative[start] / total),
        "fit_end_db": 10.0 * math.log10(cumulative[end] / total) if cumulative[end] > 0 else None,
        "fit_points": len(points),
        "r_squared": r_squared,
        "slope_db_per_second": slope,
        "terminal_power_window_seconds": guard_frames / rate,
        "terminal_power_drop_from_peak_10ms_db": terminal_drop,
        "terminal_window_is_exact_silence": terminal_power == 0,
        "capture_after_fit_seconds": time_after_fit,
        "estimated_omitted_energy_fraction_at_fit_end": omitted_fraction,
    })
    if terminal_drop is not None and terminal_drop < MINIMUM_TERMINAL_POWER_DROP_DB:
        result["reason"] = "Insufficient captured terminal-envelope decay; need at least 35 dB support."
    elif time_after_fit < max(0.05, fit_span * 0.25):
        result["reason"] = "Capture ends too close to the -25 dB crossing; EDC may be truncated."
    elif omitted_fraction is None or omitted_fraction > MAXIMUM_OMITTED_ENERGY_FRACTION:
        result["reason"] = "Estimated unrecorded tail exceeds 5% of EDC energy at fit end."
    else:
        result.update({"accepted": True, "reason": None, "t20_seconds": -20.0 / slope,
                       "extrapolated_rt60_seconds": rt60,
                       "fit_quality_note": "Approximately linear." if r_squared >= 0.95 else
                       "Low linearity: this spectrum does not follow a single exponential decay."})
    return result


def describe_component(name: str) -> tuple[str, bool]:
    if name.startswith("binaural-"):
        return "Measured HRIR on one propagated path; filter ringdown is not room RT60.", False
    if name.startswith("l-corridor-"):
        return "Single portal path with no diffuse return; filtered decay is not room RT60.", False
    if name.endswith("-audition"):
        return "Finite noise burst; not an impulse response.", False
    if name.endswith("-direct"):
        return "Direct path only; filtered decay is filter ringdown, not room RT60.", False
    if name.endswith("-early"):
        return "Early reflections only; fitted decay is not a diffuse room RT60.", False
    if "open-boundaries" in name:
        return "Open boundaries; expected wet excitation is zero, so filter ringdown is not reverb.", False
    if name.endswith("-late"):
        return "Isolated late return; broad spectral decay estimate.", True
    return "Mixed impulse response; fit includes direct/early energy and may not isolate late decay.", True


def analyze_file(path: Path, probe_case: dict[str, Any]) -> dict[str, Any]:
    rate, samples = parse_float_wave(path.read_bytes())
    peak = max((abs(value) for value in samples), default=0.0)
    first = next((i // 2 for i, value in enumerate(samples) if value != 0.0), None)
    threshold = max(1e-12, peak * 1e-9)
    significant = next((i // 2 for i, value in enumerate(samples) if abs(value) > threshold), None)
    component, reverb_eligible = describe_component(path.stem)
    result: dict[str, Any] = {
        "file": path.name, "sample_rate": rate, "frames": len(samples) // 2,
        "capture_seconds": len(samples) / (2 * rate), "peak_amplitude": peak,
        "first_nonzero_frame": first, "first_nonzero_seconds": first / rate if first is not None else None,
        "first_significant_frame": significant,
        "first_significant_seconds": significant / rate if significant is not None else None,
        "significant_amplitude_threshold": threshold,
        "component_note": component, "room_reverb_estimate_eligible": reverb_eligible,
        "onset_note": "Capture-relative timestamps; capture/system latency is not subtracted.",
        "independent_fdn_component_targets_seconds": {
            "low": probe_case.get("targetLowRt60"), "mid": probe_case.get("targetMidRt60"),
            "high": probe_case.get("targetHighRt60")},
        "bands": [],
    }
    for centre in BAND_CENTRES_HZ:
        band_result: dict[str, Any] = {"centre_hz": centre, "q": BAND_Q}
        try:
            band_result.update(fit_decay(band_energy(samples, rate, centre), rate))
        except ValueError as exc:
            band_result.update({"accepted": False, "reason": str(exc)})
        result["bands"].append(band_result)
    return result


def analyze_directory(directory: Path, output_directory: Path) -> dict[str, Any]:
    paths = sorted(path for path in directory.glob("*.wav") if not path.stem.endswith("-audition"))
    if not paths:
        raise ValueError(f"No impulse WAV files found in {directory}.")
    probe_report_path = directory / "report.json"
    probe_report = json.loads(probe_report_path.read_text(encoding="utf-8-sig")) if probe_report_path.exists() else {}
    probe_cases = {case["name"]: case for case in probe_report.get("cases", [])}
    result: dict[str, Any] = {
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source_directory": str(directory.resolve()), "method": METHOD_NOTE,
        "capture_support_policy": "Require 35 dB terminal-envelope drop, post-fit capture guard, and <=5% estimated omitted energy at -25 dB. No noise subtraction or unrecorded-tail correction.",
        "cases": [], "errors": [], "component_onsets": [],
    }
    for path in paths:
        try:
            result["cases"].append(analyze_file(path, probe_cases.get(path.stem, {})))
        except (OSError, ValueError, struct.error) as exc:
            result["errors"].append({"file": path.name, "error": str(exc)})
    cases = {Path(case["file"]).stem: case for case in result["cases"]}
    for name, late in cases.items():
        if not name.endswith("-late"):
            continue
        direct = cases.get(name[:-5] + "-direct")
        if direct is None:
            continue
        direct_time, late_time = direct["first_nonzero_seconds"], late["first_nonzero_seconds"]
        result["component_onsets"].append({
            "room_prefix": name[:-5], "direct_first_nonzero_seconds": direct_time,
            "late_first_nonzero_seconds": late_time,
            "late_minus_direct_ms": (late_time - direct_time) * 1000
            if late_time is not None and direct_time is not None else None,
            "note": "Separate captures; difference assumes matching startup/capture timing. Mixed WAV alone cannot isolate late onset."
        })
    output_directory.mkdir(parents=True, exist_ok=True)
    (output_directory / "analysis.json").write_text(
        json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    with (output_directory / "analysis.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["file", "centre_hz", "accepted", "room_reverb_estimate_eligible",
                         "t20_seconds", "extrapolated_rt60_seconds", "r_squared", "reason"])
        for case in result["cases"]:
            for band in case["bands"]:
                writer.writerow([case["file"], band["centre_hz"], band["accepted"],
                                 case["room_reverb_estimate_eligible"], band.get("t20_seconds"),
                                 band.get("extrapolated_rt60_seconds"), band.get("r_squared"), band.get("reason")])
    return result


def self_test() -> None:
    rate, target = 48000, 0.9
    samples = array("f")
    for frame in range(rate * 3):
        sample = math.sin(2 * math.pi * 1000 * frame / rate) * 10 ** (-3 * frame / (rate * target))
        samples.extend((sample, sample))
    decay = fit_decay(band_energy(samples, rate, 1000), rate)
    assert decay["accepted"], decay
    assert abs(decay["extrapolated_rt60_seconds"] - target) < target * 0.05, decay
    assert decay["r_squared"] > 0.98, decay
    short_energy = array("d", (10 ** (-6 * i / (rate * 20)) for i in range(rate // 4)))
    assert not fit_decay(short_energy, rate)["accepted"], "Truncated slow tail must be rejected."
    assert not fit_decay(array("d", [0.0] * 1000), rate)["accepted"]
    payload = struct.pack("<ffff", 0.0, 0.0, 0.5, -0.25)
    raw = (b"RIFF" + struct.pack("<I", 36 + len(payload)) + b"WAVEfmt " + struct.pack("<IHHIIHH",
           16, 3, 2, rate, rate * 8, 8, 32) + b"data" + struct.pack("<I", len(payload)) + payload)
    parsed_rate, parsed = parse_float_wave(raw)
    assert parsed_rate == rate and list(parsed) == [0.0, 0.0, 0.5, -0.25]
    try:
        parse_float_wave(raw[:-1])
    except ValueError:
        pass
    else:
        raise AssertionError("Truncated WAV must be rejected.")
    print("Self-test passed: known exponential decay, insufficient capture, silence, and float-WAV parser.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("directory", nargs="?", type=Path, default=DEFAULT_DIRECTORY)
    parser.add_argument("--output-directory", type=Path)
    parser.add_argument("--self-test", action="store_true", help="Run analytical checks and exit without writing artifacts.")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    try:
        output_directory = args.output_directory or args.directory
        result = analyze_directory(args.directory, output_directory)
    except (OSError, ValueError) as exc:
        print(f"Analysis failed: {exc}", file=sys.stderr)
        return 1
    accepted = sum(band["accepted"] for case in result["cases"] for band in case["bands"])
    print(f"Analyzed {len(result['cases'])} WAVs; {accepted} spectral fits have sufficient captured support.")
    print(f"Outputs: {output_directory / 'analysis.json'} and {output_directory / 'analysis.csv'}")
    print("RT60 values are T20 extrapolations of filtered spectra; see component eligibility and limitations.")
    for error in result["errors"]:
        print(f"Rejected {error['file']}: {error['error']}", file=sys.stderr)
    return 1 if result["errors"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
