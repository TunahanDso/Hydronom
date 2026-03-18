using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Hydronom.Core.Domain.AI;
using Hydronom.Core.Interfaces.AI;

namespace Hydronom.Runtime.AI
{
    /// <summary>
    /// Runtime içindeki IAiTool implementasyonlarını kayıt eder ve
    /// LLM'e verilecek ToolSpec listesini üretir.
    ///
    /// Amaç:
    /// - Tool'ları tek yerde toplayıp isimle erişmek
    /// - AiGateway tarafına "hangi tool'lar var" bilgisini vermek
    /// </summary>
    public sealed class ToolRegistry
    {
        // Tool adı -> tool instance
        private readonly Dictionary<string, IAiTool> _toolsByName =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tool kaydı ekler. Aynı isim varsa hata fırlatır.
        /// </summary>
        public void Register(IAiTool tool)
        {
            ArgumentNullException.ThrowIfNull(tool);

            if (tool.Spec is null)
                throw new ArgumentException("Tool.Spec null olamaz.", nameof(tool));

            var name = NormalizeToolName(tool.Spec.Name, nameof(tool));

            if (_toolsByName.ContainsKey(name))
                throw new InvalidOperationException($"ToolRegistry: '{name}' zaten kayıtlı.");

            _toolsByName[name] = tool;
        }

        /// <summary>
        /// Birden fazla tool'u sırayla kaydeder.
        /// </summary>
        public void RegisterRange(IEnumerable<IAiTool> tools)
        {
            ArgumentNullException.ThrowIfNull(tools);

            foreach (var tool in tools)
                Register(tool);
        }

        /// <summary>
        /// Tool varsa döndürür, yoksa false.
        /// </summary>
        public bool TryGet(string toolName, out IAiTool tool)
        {
            tool = default!;

            if (string.IsNullOrWhiteSpace(toolName))
                return false;

            return _toolsByName.TryGetValue(toolName.Trim(), out tool!);
        }

        /// <summary>
        /// Tool'u zorunlu olarak getirir. Yoksa hata fırlatır.
        /// </summary>
        public IAiTool GetRequired(string toolName)
        {
            if (!TryGet(toolName, out var tool))
                throw new KeyNotFoundException($"ToolRegistry: '{toolName}' adlı tool bulunamadı.");

            return tool;
        }

        /// <summary>
        /// Kayıtlı tüm tool'ları döndürür.
        /// </summary>
        public IReadOnlyList<IAiTool> GetAllTools()
        {
            var list = _toolsByName.Values
                .OrderBy(t => t.Spec.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ReadOnlyCollection<IAiTool>(list);
        }

        /// <summary>
        /// LLM'e verilecek ToolSpec listesini döndürür.
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
        /// Kayıtlı tool adlarını deterministik sıralı döndürür.
        /// </summary>
        public IReadOnlyList<string> GetAllToolNames()
        {
            var list = _toolsByName.Keys
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ReadOnlyCollection<string>(list);
        }

        /// <summary>
        /// Belirli isimde tool kayıtlı mı?
        /// </summary>
        public bool Contains(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return false;

            return _toolsByName.ContainsKey(toolName.Trim());
        }

        /// <summary>
        /// Toplam tool sayısı.
        /// </summary>
        public int Count => _toolsByName.Count;

        private static string NormalizeToolName(string? value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ToolSpec.Name boş olamaz.", paramName);

            return value.Trim();
        }
    }
}