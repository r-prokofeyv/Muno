# PhaseLimiter integration

Repository:
https://github.com/ai-mastering/phaselimiter

Muno integrates with PhaseLimiter through phase_limiter.exe.

Supported mastering mode:
--mastering=true
--mastering_mode=mastering5


## User-configurable parameters

Target Loudness

--reference_mode=loudness
--reference=<double>

Muno range: -20 ... 0
Muno default: -9


Mastering Intensity

--mastering5_mastering_level=<double>

Muno range: 0.0 ... 1.0
Muno default: 1.0


Preserve Bass

--erb_eval_func_weighting=<bool>

Default: false


Optimization Algorithm

--mastering5_optimization_algorithm=<string>

Values:
de
nm
pso
de_prmm
pso_dv

Default:
de_prmm


True Peak Ceiling

--ceiling=<double>

Muno range:
-6 ... 0

Muno default:
0


## Output

--output_format=<wav|mp3|aac>

WAV:
--bit_depth=16
--bit_depth=24
--bit_depth=32

MP3:
PhaseLimiter encodes at 320 kbps.

AAC:
PhaseLimiter encodes at 256 kbps.

--sample_rate=<integer>

Muno presets:
44100
48000
88200
96000

PhaseLimiter DSP processing is internally performed at 44100 Hz.
sample_rate controls the output sample rate.


## Fixed Muno engine parameters

--mastering=true
--mastering_mode=mastering5
--mastering5_optimization_max_eval_count=40000

--pre_compression=true
--pre_compression_threshold=6
--pre_compression_mean_sec=0.2

--low_cut_freq=20
--high_cut_freq=20000

--limiting_mode=phase
--ceiling_mode=true_peak
--true_peak_oversample=4


## Required resources

phase_limiter.exe
ffmpeg.exe
sound_quality2_cache

All paths passed to PhaseLimiter must be absolute.

Input:
--input=<absolute path>

Output:
--output=<absolute path>

FFmpeg:
--ffmpeg=<absolute path>

Cache:
--sound_quality2_cache=<absolute path>

Temp:
--tmp=<absolute writable directory>


## Process execution

Run one phase_limiter.exe process per mastering job.

Use ProcessStartInfo.ArgumentList.

Use:
UseShellExecute = false
CreateNoWindow = true
RedirectStandardOutput = true
RedirectStandardError = true


## Progress

PhaseLimiter writes progress to stdout:

progression: <value>

value range:
0.0 ... 1.0

Parse the number using InvariantCulture.

Successful completion is determined by:

process has exited
AND ExitCode == 0
AND output file exists

Do not use progression: 1 as the completion condition.


## stderr

PhaseLimiter writes normal diagnostic output to stderr.

stderr output alone is not an error.

Store stderr for logging and diagnostics.


## Cancellation

When cancellation is required, terminate the complete process tree:

process.Kill(entireProcessTree: true)

PhaseLimiter may launch FFmpeg as a child process.