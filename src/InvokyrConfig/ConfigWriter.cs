using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace InvokyrConfig
{
    /// <summary>
    /// Batches saves while the panel is open: each file's SaveOnConfigSet is off (a slider drag would otherwise rewrite
    /// the file every frame), changed files are saved once a second and again on close, and SaveOnConfigSet is put back.
    /// </summary>
    internal sealed class ConfigWriter
    {
        private const float Interval = 1f;

        private readonly Action<ConfigFile> _save;
        private readonly Action<ConfigFile, Exception> _onError;
        private readonly Dictionary<ConfigFile, bool> _priorSaveOnSet = new Dictionary<ConfigFile, bool>();
        private readonly HashSet<ConfigFile> _dirty = new HashSet<ConfigFile>();
        private float? _lastFlush;

        public ConfigWriter(Action<ConfigFile> save = null, Action<ConfigFile, Exception> onError = null)
        {
            _save = save ?? (f => f.Save());
            _onError = onError;
        }

        public void Begin(IEnumerable<ConfigFile> files)
        {
            End();
            foreach (var file in files)
            {
                if (file == null || _priorSaveOnSet.ContainsKey(file))
                    continue;
                _priorSaveOnSet[file] = file.SaveOnConfigSet;
                file.SaveOnConfigSet = false;
            }
            _lastFlush = null;
        }

        public void MarkDirty(ConfigFile file)
        {
            if (file != null)
                _dirty.Add(file);
        }

        public void Tick(float now)
        {
            if (_lastFlush == null)
                _lastFlush = now;
            if (now - _lastFlush.Value < Interval)
                return;
            _lastFlush = now;
            Flush();
        }

        public void End()
        {
            Flush();
            foreach (var pair in _priorSaveOnSet)
                pair.Key.SaveOnConfigSet = pair.Value;
            _priorSaveOnSet.Clear();
        }

        private void Flush()
        {
            if (_dirty.Count == 0)
                return;
            var files = new List<ConfigFile>(_dirty);
            _dirty.Clear();
            foreach (var file in files)
            {
                try
                {
                    _save(file);
                }
                catch (Exception e)
                {
                    _onError?.Invoke(file, e);
                }
            }
        }
    }
}
