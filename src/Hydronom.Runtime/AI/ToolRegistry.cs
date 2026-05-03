using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Hydronom.Core.Domain.AI;
using Hydronom.Core.Interfaces.AI;

namespace Hydronom.Runtime.AI
{
    /// <summary>
    /// Runtime iÃ§indeki IAiTool implementasyonlarÄ±nÄ± kayÄ±t eder ve
    /// LLM'e verilecek ToolSpec listesini Ã¼retir.
    ///
    /// AmaÃ§:
    /// - Tool'larÄ± tek yerde toplayÄ±p isimle eriÅŸmek
    /// - AiGateway tarafÄ±na "hangi tool'lar var" bilgisini vermek
    /// </summary>
    public sealed class ToolRegistry
    {
        // Tool adÄ± -> tool instance
        private readonly Dictionary<string, IAiTool> _toolsByName =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tool kaydÄ± ekler. AynÄ± isim varsa hata fÄ±rlatÄ±r.
        /// </summary>
        public void Register(IAiTool tool)
        {
            ArgumentNullException.ThrowIfNull(tool);

            if (tool.Spec is null)
                throw new ArgumentException("Tool.Spec null olamaz.", nameof(tool));

            var name = NormalizeToolName(tool.Spec.Name, nameof(tool));

            if (_toolsByName.ContainsKey(name))
                throw new InvalidOperationException($"ToolRegistry: '{name}' zaten kayÄ±tlÄ±.");

            _toolsByName[name] = tool;
        }

        /// <summary>
        /// Birden fazla tool'u sÄ±rayla kaydeder.
        /// </summary>
        public void RegisterRange(IEnumerable<IAiTool> tools)
        {
            ArgumentNullException.ThrowIfNull(tools);

            foreach (var tool in tools)
                Register(tool);
        }

        /// <summary>
        /// Tool varsa dÃ¶ndÃ¼rÃ¼r, yoksa false.
        /// </summary>
        public bool TryGet(string toolName, out IAiTool tool)
        {
            tool = default!;

            if (string.IsNullOrWhiteSpace(toolName))
                return false;

            return _toolsByName.TryGetValue(toolName.Trim(), out tool!);
        }

        /// <summary>
        /// Tool'u zorunlu olarak getirir. Yoksa hata fÄ±rlatÄ±r.
        /// </summary>
        public IAiTool GetRequired(string toolName)
        {
            if (!TryGet(toolName, out var tool))
                throw new KeyNotFoundException($"ToolRegistry: '{toolName}' adlÄ± tool bulunamadÄ±.");

            return tool;
        }

        /// <summary>
        /// KayÄ±tlÄ± tÃ¼m tool'larÄ± dÃ¶ndÃ¼rÃ¼r.
        /// </summary>
        public IReadOnlyList<IAiTool> GetAllTools()
        {
            var list = _toolsByName.Values
                .OrderBy(t => t.Spec.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ReadOnlyCollection<IAiTool>(list);
        }

        /// <summary>
        /// LLM'e verilecek ToolSpec listesini dÃ¶ndÃ¼rÃ¼r.
        /// </summary>
        public IReadOnlyList<ToolSpec> GetAllToolSpecs()
        {
            var list = _toolsByName.Values
                .Select(t => t.Spec)
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ReadOnlyCollection<ToolSpec>(list);
        }

        /// <summary>
        /// KayÄ±tlÄ± tool adlarÄ±nÄ± deterministik sÄ±ralÄ± dÃ¶ndÃ¼rÃ¼r.
        /// </summary>
        public IReadOnlyList<string> GetAllToolNames()
        {
            var list = _toolsByName.Keys
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ReadOnlyCollection<string>(list);
        }

        /// <summary>
        /// Belirli isimde tool kayÄ±tlÄ± mÄ±?
        /// </summary>
        public bool Contains(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return false;

            return _toolsByName.ContainsKey(toolName.Trim());
        }

        /// <summary>
        /// Toplam tool sayÄ±sÄ±.
        /// </summary>
        public int Count => _toolsByName.Count;

        private static string NormalizeToolName(string? value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ToolSpec.Name boÅŸ olamaz.", paramName);

            return value.Trim();
        }
    }
}
