using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Puck.Diagnostics;

namespace Puck.Audio;

/// 효과음. 착지하면서 말하고 동시에 클릭에 반응하는 일이 실제로 있으므로
/// **겹쳐 재생**할 수 있어야 한다 — 믹서 하나에 소리를 얹는다. mac의
/// 플레이어 노드 풀에 해당한다.
public sealed class SfxPlayer : IDisposable
{
    /// 한 번에 얹을 수 있는 소리의 수. 넘으면 새 소리를 버린다 — 늘어난
    /// 지연으로 들리는 것보다 낫고, 어차피 사람 귀에 셋 이상은 소음이다.
    public const int MaxConcurrent = 8;

    private readonly WaveOutEvent _output = new();
    private readonly MixingSampleProvider _mixer;
    private readonly object _gate = new();
    private bool _disposed;

    public SfxPlayer(int sampleRate = 44100, int channels = 2)
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels))
        {
            // 소리가 하나도 없을 때 믹서가 끝났다고 말하면 출력이 멈춘다.
            ReadFully = true,
        };
        _output.Init(_mixer);
        _output.Play();
    }

    /// 소리를 낼 것인가. 집중 지원 중에는 조용히 있는 것이 예의다.
    public Func<bool>? IsMuted { get; set; }

    public void Play(string? filePath, float volume = 1.0f)
    {
        if (_disposed || filePath is null) return;
        if (IsMuted?.Invoke() == true) return;
        if (!File.Exists(filePath)) return;

        try
        {
            var reader = new AudioFileReader(filePath) { Volume = volume };
            var resampled = Resample(reader);

            lock (_gate)
            {
                if (_mixer.MixerInputs.Count() >= MaxConcurrent)
                {
                    reader.Dispose();
                    return;
                }
                _mixer.AddMixerInput(resampled);
            }
        }
        catch (Exception ex)
        {
            // 소리 하나가 깨졌다고 앱이 멈추면 안 된다. 아바타 패키지의
            // wav가 손상됐거나 형식이 이상한 것은 사람이 고칠 수 있는 일이다.
            AppLogger.Warning("audio", "효과음을 재생하지 못했습니다",
                new Dictionary<string, object?> { ["file"] = Path.GetFileName(filePath), ["error"] = ex.Message });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _output.Stop();
        _output.Dispose();
    }

    /// 믹서는 형식이 하나여야 한다. 아바타가 들고 온 wav가 어떤 것이든
    /// 거기 맞춘다 — 사람에게 "44.1kHz 스테레오로 저장하세요"라고 할 수는 없다.
    private ISampleProvider Resample(AudioFileReader reader)
    {
        ISampleProvider sample = reader;

        if (sample.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
            sample = new MonoToStereoSampleProvider(sample);

        if (sample.WaveFormat.SampleRate != _mixer.WaveFormat.SampleRate)
            sample = new WdlResamplingSampleProvider(sample, _mixer.WaveFormat.SampleRate);

        return sample;
    }
}
