using CommunityToolkit.Mvvm.ComponentModel;
using DAW.Wave.Services.Effects;
using DAW.Wave.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace DAW.ViewModels.Effects;

public class GraphicEQEffectViewModel : ObservableRecipient
{
    private readonly GraphicEQEffect _effect;

    public ObservableCollection<EQBand> Bands { get; } = [];

    public GraphicEQEffectViewModel(IAudioEffect effect)
    {
        _effect = effect as GraphicEQEffect;

        string[] labels = ["31Hz", "62Hz", "125Hz", "250Hz", "500Hz", "1kHz", "2kHz", "4kHz", "8kHz", "16kHz"];

        for (int i = 0; i < labels.Length; i++)
        {
            var band = new EQBand(labels[i], i, _effect[i]); // 传递频段标签和初始增益

            // 使用 SetProperty 处理增益变更
            band.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(EQBand.Gain))
                    _effect[band.BandIndex] = band.Gain; // 更新增益
            };

            Bands.Add(band);
        }
    }
}

public partial class EQBand : ObservableRecipient
{
    [ObservableProperty]
    public partial float Gain { get; set; }

    public string Label { get; set; }
    public int BandIndex { get; set; }

    public EQBand(string label, int bandIndex, float initialGain)
    {
        Label = label;
        BandIndex = bandIndex;
        Gain = initialGain;
    }
}

