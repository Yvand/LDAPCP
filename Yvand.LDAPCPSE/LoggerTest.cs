using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yvand.LdapClaimsProvider
{
    //[System.Runtime.InteropServices.GuidAttribute("5C5CE453-21C6-4B2B-8AFD-328F88282099")]
    public class Logger : SPDiagnosticsServiceBase
    {
        static string DiagnosticsAreaName = LDAPCPSE.ClaimsProviderName;
        public Logger() : base(DiagnosticsAreaName, SPFarm.Local) { }
        public Logger(string name, SPFarm farm) : base(name, farm) { }

        public override string DisplayName { get { return DiagnosticsAreaName; } }

        private static Logger _current;
        public static Logger Local
        {
            get
            {
                if (_current == null)
                {
                    _current = SPDiagnosticsServiceBase.GetLocal<Logger>();
                    if (_current == null)
                    {
                        SPSecurity.RunWithElevatedPrivileges(delegate ()
                        {
                            // otherwise instantiate and register the new instance, which requires farm administrator privileges
                            _current = new Logger();
                            //svc.Update();
                        });
                    }
                }
                return _current;
            }
        }

        public static void Unregister()
        {
            SPFarm.Local.Services
                        .OfType<Logger>()
                        .ToList()
                        .ForEach(s =>
                        {
                            s.Delete();
                            s.Unprovision();
                            s.Uncache();
                        });
        }

        public static void Log(string message, TraceSeverity traceSeverity, EventSeverity eventSeverity, TraceCategory category)
        {
            try
            {
                WriteTrace(category, traceSeverity, message);
            }
            catch (Exception ex)
            {   // Don't want to do anything if logging goes wrong, just ignore and continue
                Debug.WriteLine($"Unable to record message in SharePoint logs: {ex.Message}");
            }
        }

        public static void LogException(string claimsProviderName, string customMessage, TraceCategory category, Exception ex)
        {
            try
            {
                string errorNessage = String.Empty;
                if (ex is AggregateException)
                {
                    StringBuilder message = new StringBuilder($"[{claimsProviderName}] Unexpected error(s) {customMessage}:");
                    string excetpionMessage = Environment.NewLine + "[EXCEPTION {0}]: {1}: {2}";
                    var aggEx = ex as AggregateException;
                    int count = 1;
                    foreach (var innerEx in aggEx.InnerExceptions)
                    {
                        string currentMessage;
                        if (innerEx.InnerException != null)
                        {
                            currentMessage = String.Format(excetpionMessage, count++.ToString(), innerEx.InnerException.GetType().Name, innerEx.InnerException.Message);
                        }
                        else
                        {
                            currentMessage = String.Format(excetpionMessage, count++.ToString(), innerEx.GetType().Name, innerEx.Message);
                        }
                        message.Append(currentMessage);
                    }
                    errorNessage = message.ToString();
                }
                else if (ex is FileNotFoundException)
                {
                    string stackTrace = String.Empty;
                    try
                    {
                        stackTrace = ex.StackTrace;
                    }
                    catch { } // Calling property FileNotFoundException.StackTrace may thrown an exception
                    errorNessage = $"[{claimsProviderName}] .NET could not load an assembly, please check your assembly bindings in machine.config file, or .config file for current process. Exception details: '{ex.Message}'";
                    if (!String.IsNullOrWhiteSpace(stackTrace))
                    {
                        errorNessage += $"{Environment.NewLine}Callstack: {stackTrace}";
                    }
                }
                else if (ex is DirectoryServicesCOMException)
                {
                    errorNessage = "[{0}] Unexpected error {1}: {2}: {3}";
                    errorNessage = String.Format(errorNessage, claimsProviderName, customMessage, ex.GetType().Name, ((DirectoryServicesCOMException)ex).ExtendedErrorMessage);
                }
                else
                {
                    errorNessage = "[{0}] Unexpected error {1}: {2}: {3}";
                    if (ex.InnerException != null && !(ex.InnerException is AggregateException))
                    {
                        errorNessage = String.Format(errorNessage, claimsProviderName, customMessage, ex.InnerException.GetType().Name, ex.InnerException.Message);
                    }
                    else
                    {
                        errorNessage = String.Format(errorNessage, claimsProviderName, customMessage, ex.GetType().Name, ex.Message);
                    }
                }
                WriteTrace(category, TraceSeverity.Unexpected, errorNessage);
            }
            catch (Exception logex)
            {   // Don't want to do anything if logging goes wrong, just ignore and continue
                Debug.WriteLine($"Unable to record message in SharePoint logs: {logex.Message}");
            }
        }

        /// <summary>
        /// Record message (in VerboseEx) only if assembly is compiled in debug mode
        /// </summary>
        /// <param name="message"></param>
        public static void LogDebug(string message)
        {
            try
            {
#if DEBUG
                WriteTrace(TraceCategory.Debug, TraceSeverity.High, message);
                Debug.WriteLine(message);
#else
                WriteTrace(TraceCategory.Debug, TraceSeverity.VerboseEx, message);
                Debug.WriteLine(message);
#endif
            }
            catch (Exception logex)
            {   // Don't want to do anything if logging goes wrong, just ignore and continue
                Debug.WriteLine($"Unable to record message in SharePoint logs: {logex.Message}");
            }
        }

        public static void WriteTrace(TraceCategory Category, TraceSeverity Severity, string message)
        {
            Local.WriteTrace(1337, Local.GetCategory(Category), Severity, message);
        }

        public static void WriteEvent(TraceCategory Category, EventSeverity Severity, string message)
        {
            Local.WriteEvent(1337, Local.GetCategory(Category), Severity, message);
        }

        #region Init
        protected override IEnumerable<SPDiagnosticsArea> ProvideAreas()
        {
            yield return Area;
        }

        private static SPDiagnosticsArea Area
        {
            get
            {
                return new SPDiagnosticsArea(
                    DiagnosticsAreaName,
                    new List<SPDiagnosticsCategory>
                    {
                        CreateCategory(TraceCategory.Claims_Picking),
                        CreateCategory(TraceCategory.Configuration),
                        CreateCategory(TraceCategory.Lookup),
                        CreateCategory(TraceCategory.Core),
                        CreateCategory(TraceCategory.Augmentation),
                        CreateCategory(TraceCategory.Rehydration),
                        CreateCategory(TraceCategory.Debug),
                        CreateCategory(TraceCategory.Custom),
                        CreateCategory(TraceCategory.AzureIdentity),
                        CreateCategory(TraceCategory.GraphRequests),
                    }
                );
            }
        }

        private static SPDiagnosticsCategory CreateCategory(TraceCategory category)
        {
            return new SPDiagnosticsCategory(
                        GetCategoryName(category),
                        GetCategoryDefaultTraceSeverity(category),
                        GetCategoryDefaultEventSeverity(category)
                    );
        }

        private SPDiagnosticsCategory GetCategory(TraceCategory cat)
        {
            return base.Areas[DiagnosticsAreaName].Categories[GetCategoryName(cat)];
        }

        private static string GetCategoryName(TraceCategory cat)
        {
            // Get the type
            Type type = cat.GetType();
            // Get fieldinfo for this type
            System.Reflection.FieldInfo fieldInfo = type.GetField(cat.ToString());
            // Get the stringvalue attributes
            CategoryNameAttribute[] attribs = fieldInfo.GetCustomAttributes(typeof(CategoryNameAttribute), false) as CategoryNameAttribute[];
            // Return the first if there was a match.
            return attribs.Length > 0 ? attribs[0].Name : null;
        }

        private static TraceSeverity GetCategoryDefaultTraceSeverity(TraceCategory cat)
        {
            // Get the type
            Type type = cat.GetType();
            // Get fieldinfo for this type
            System.Reflection.FieldInfo fieldInfo = type.GetField(cat.ToString());
            // Get the stringvalue attributes
            DefaultTraceSeverityAttribute[] attribs = fieldInfo.GetCustomAttributes(typeof(DefaultTraceSeverityAttribute), false) as DefaultTraceSeverityAttribute[];
            // Return the first if there was a match.
            return attribs.Length > 0 ? attribs[0].Severity : TraceSeverity.Unexpected;
        }

        private static EventSeverity GetCategoryDefaultEventSeverity(TraceCategory cat)
        {
            // Get the type
            Type type = cat.GetType();
            // Get fieldinfo for this type
            System.Reflection.FieldInfo fieldInfo = type.GetField(cat.ToString());
            // Get the stringvalue attributes
            DefaultEventSeverityAttribute[] attribs = fieldInfo.GetCustomAttributes(typeof(DefaultEventSeverityAttribute), false) as DefaultEventSeverityAttribute[];
            // Return the first if there was a match.
            return attribs.Length > 0 ? attribs[0].Severity : EventSeverity.Error;
        }
        #endregion
    }
}
