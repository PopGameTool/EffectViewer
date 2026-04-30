using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using EffectViewer.TodLib.Common;

namespace EffectViewer.ViewModels
{
    public sealed partial class FloatParameterTrackViewModel : ViewModelBase
    {
        private readonly float _defaultValue;
        private bool _suppressChanged;

        public string Name { get; }
        public ObservableCollection<FloatParameterTrackNodeViewModel> Nodes { get; } = [];

        public event Action<FloatParameterTrackViewModel> Changed;

        public FloatParameterTrackViewModel(string name, float defaultValue)
        {
            Name = name;
            _defaultValue = defaultValue;
        }

        public void LoadFrom(FloatParameterTrack track)
        {
            _suppressChanged = true;
            Nodes.Clear();

            if (track?.mNodes is not null && track.mCountNodes > 0)
            {
                int count = Math.Min(track.mCountNodes, track.mNodes.Length);
                for (int i = 0; i < count; i++)
                {
                    AddNode(CreateNode(track.mNodes[i]));
                }
            }

            if (Nodes.Count == 0)
            {
                AddNode(CreateDefaultNode());
            }

            _suppressChanged = false;
        }

        public void ApplyTo(FloatParameterTrack track)
        {
            if (track is null)
            {
                return;
            }

            FloatParameterTrackNode[] nodes = Nodes
                .OrderBy(node => node.TimePercent)
                .Select(ToTrackNode)
                .ToArray();

            track.mNodes = nodes;
            track.mCountNodes = nodes.Length;
        }

        public void LoadFromText(string text)
        {
            FloatParameterTrack track = new();
            string source = string.IsNullOrWhiteSpace(text)
                ? _defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : text;
            DefinitionMapLoader.ReadFloatTrack(source, track);
            LoadFrom(track);
        }

        [RelayCommand]
        private void AddNode()
        {
            double time = Nodes.Count == 0
                ? 0d
                : Math.Min(100d, Nodes[^1].TimePercent + 10d);
            AddNode(new FloatParameterTrackNodeViewModel(RemoveNode, CopyNode, EqualizeNode)
            {
                TimePercent = time,
                LowValue = _defaultValue,
                HighValue = _defaultValue,
                CurveType = TodCurves.Linear,
                Distribution = TodCurves.Linear
            });
            RaiseChanged();
        }

        [RelayCommand]
        private void Reset()
        {
            _suppressChanged = true;
            foreach (FloatParameterTrackNodeViewModel node in Nodes.ToArray())
            {
                node.Changed -= OnNodeChanged;
            }

            Nodes.Clear();
            AddNode(CreateDefaultNode());
            _suppressChanged = false;
            RaiseChanged();
        }

        private void AddNode(FloatParameterTrackNodeViewModel node)
        {
            node.Changed += OnNodeChanged;
            Nodes.Add(node);
        }

        private void CopyNode(FloatParameterTrackNodeViewModel node)
        {
            if (node is null)
            {
                return;
            }

            AddNode(new FloatParameterTrackNodeViewModel(RemoveNode, CopyNode, EqualizeNode)
            {
                TimePercent = Math.Min(100d, node.TimePercent + 1d),
                LowValue = node.LowValue,
                HighValue = node.HighValue,
                CurveType = node.CurveType,
                Distribution = node.Distribution
            });
            RaiseChanged();
        }

        private void EqualizeNode(FloatParameterTrackNodeViewModel node)
        {
            if (node is null)
            {
                return;
            }

            node.HighValue = node.LowValue;
        }

        private void RemoveNode(FloatParameterTrackNodeViewModel node)
        {
            if (node is null || Nodes.Count <= 1)
            {
                return;
            }

            node.Changed -= OnNodeChanged;
            Nodes.Remove(node);
            RaiseChanged();
        }

        private void OnNodeChanged(FloatParameterTrackNodeViewModel node)
        {
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (!_suppressChanged)
            {
                SortNodesByTime();
                Changed?.Invoke(this);
            }
        }

        private void SortNodesByTime()
        {
            FloatParameterTrackNodeViewModel[] sorted = Nodes.OrderBy(node => node.TimePercent).ToArray();
            for (int i = 0; i < sorted.Length; i++)
            {
                int currentIndex = Nodes.IndexOf(sorted[i]);
                if (currentIndex != i)
                {
                    Nodes.Move(currentIndex, i);
                }
            }
        }

        private FloatParameterTrackNodeViewModel CreateDefaultNode()
        {
            return new FloatParameterTrackNodeViewModel(RemoveNode, CopyNode, EqualizeNode)
            {
                TimePercent = 0d,
                LowValue = _defaultValue,
                HighValue = _defaultValue,
                CurveType = TodCurves.Constant,
                Distribution = TodCurves.Linear
            };
        }

        private FloatParameterTrackNodeViewModel CreateNode(FloatParameterTrackNode node)
        {
            return new FloatParameterTrackNodeViewModel(RemoveNode, CopyNode, EqualizeNode)
            {
                TimePercent = node.mTime * 100d,
                LowValue = node.mLowValue,
                HighValue = node.mHighValue,
                CurveType = node.mCurveType,
                Distribution = node.mDistribution
            };
        }

        private static FloatParameterTrackNode ToTrackNode(FloatParameterTrackNodeViewModel node)
        {
            return new FloatParameterTrackNode
            {
                mTime = (float)(node.TimePercent / 100d),
                mLowValue = (float)node.LowValue,
                mHighValue = (float)node.HighValue,
                mCurveType = node.CurveType,
                mDistribution = node.Distribution
            };
        }
    }
}
