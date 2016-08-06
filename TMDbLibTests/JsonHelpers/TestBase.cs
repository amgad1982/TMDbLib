using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TMDbLibTests.JsonHelpers
{
    public class TestBase : IDisposable
    {
        private static readonly Regex NormalizeRegex = new Regex(@"\[[\d]+\]", RegexOptions.Compiled);
        private readonly List<ErrorEventArgs> _errors = new List<ErrorEventArgs>();

        protected readonly TestConfig Config;

        /// <summary>
        /// Ignores errors about missing C# properties (Where new or unknown JSON properties are present)
        /// </summary>
        protected bool IgnoreMissingProperties = false;

        /// <summary>
        /// Ignores errors about missing JSON properties (Where C# properties are not set)
        /// </summary>
        protected bool IgnoreMissingJson = false;

        public TestBase()
        {
            JsonSerializerSettings sett = new JsonSerializerSettings();

            //sett.MissingMemberHandling = MissingMemberHandling.Error;
            //sett.ContractResolver = new FailingContractResolver();
            //sett.Error = Error;

            Config = new TestConfig(serializer: JsonSerializer.Create(sett));
        }

        private void Error(object sender, ErrorEventArgs errorEventArgs)
        {
            _errors.Add(errorEventArgs);
            errorEventArgs.ErrorContext.Handled = true;
        }

        public void Dispose()
        {
            if (_errors.Any())
            {
                // Sort the errors
                // Also deduplicate them, as there is no point in blasting us with multiple instances of the "same" error
                Dictionary<string, ErrorEventArgs> missingFieldInCSharp = new Dictionary<string, ErrorEventArgs>();
                Dictionary<string, ErrorEventArgs> missingPropertyInJson = new Dictionary<string, ErrorEventArgs>();
                Dictionary<string, ErrorEventArgs> other = new Dictionary<string, ErrorEventArgs>();

                foreach (ErrorEventArgs error in _errors)
                {
                    string key = error.ErrorContext.Path + " / " + error.ErrorContext.Member;
                    string errorMessage = error.ErrorContext.Error.Message;

                    key = NormalizeRegex.Replace(key, "[array]");

                    if (errorMessage.StartsWith("Could not find member"))
                    {
                        // Field in JSON is missing in C#
                        if (!IgnoreMissingProperties && !missingFieldInCSharp.ContainsKey(key))
                            missingFieldInCSharp.Add(key, error);
                    }
                    else if (errorMessage.StartsWith("Required property"))
                    {
                        // Field in C# is missing in JSON
                        if (!IgnoreMissingJson && !missingPropertyInJson.ContainsKey(key))
                            missingPropertyInJson.Add(key, error);
                    }
                    else
                    {
                        if (!other.ContainsKey(key))
                            other.Add(key, error);
                    }
                }

                // Combine all errors into a nice text
                StringBuilder sb = new StringBuilder();

                if (missingFieldInCSharp.Any())
                {
                    sb.AppendLine("Fields missing in C# (Present in JSON)");
                    foreach (KeyValuePair<string, ErrorEventArgs> pair in missingFieldInCSharp)
                        sb.AppendLine($"{pair.Key}: {pair.Value.ErrorContext.Error.Message}");
                }

                if (missingPropertyInJson.Any())
                {
                    sb.AppendLine("Fields missing in JSON (Present in C#)");
                    foreach (KeyValuePair<string, ErrorEventArgs> pair in missingPropertyInJson)
                        sb.AppendLine($"{pair.Key}: {pair.Value.ErrorContext.Error.Message}");
                }

                if (other.Any())
                {
                    sb.AppendLine("Other errors");
                    foreach (KeyValuePair<string, ErrorEventArgs> pair in other)
                        sb.AppendLine($"{pair.Key}: {pair.Value.ErrorContext.Error.Message}");
                }

                if (missingFieldInCSharp.Any() || missingPropertyInJson.Any() || other.Any())
                    throw new Exception(sb.ToString());
            }
        }
    }
}