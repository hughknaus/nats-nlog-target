using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

namespace NatsStreamingServerInstaller
{
    internal enum OptionValue
    {
        None,
        Optional,
        Required
    }

    public class Option
    {
        #region "Member Variables"

        private string prototype, description;
        private Action<string> action;
        private string[] prototypes;
        private OptionValue type;

        #endregion "Member Variables"

        #region "Properties"

        public string Prototype { get { return prototype; } }
        public string Description { get { return description; } }
        public Action<string> Action { get { return action; } }
        internal string[] Prototypes { get { return prototypes; } }
        internal OptionValue OptionValue { get { return type; } }

        #endregion "Properties"

        #region "Ctor"

        public Option(string prototype, string description, Action<string> action)
        {
            this.prototype = prototype;
            this.prototypes = prototype.Split('|');
            this.description = description;
            this.action = action;
            this.type = GetOptionValue();
        }

        #endregion "Ctor"

        private OptionValue GetOptionValue()
        {
            foreach (string n in Prototypes)
            {
                if (n.IndexOf('=') >= 0)
                    return OptionValue.Required;
                if (n.IndexOf(':') >= 0)
                    return OptionValue.Optional;
            }
            return OptionValue.None;
        }

        public override string ToString()
        {
            return Prototype;
        }
    }

    public class Options : Collection<Option>
    {
        #region "Member Variables"

        private Dictionary<string, Option> options = new Dictionary<string, Option>();
        private const int optionWidth = 29;
        private static readonly Regex ValueOption = new Regex(@"^(?<flag>--|-|/)(?<name>[^:=]+)([:=](?<value>.*))?$");
        private static readonly char[] NameTerminator = new char[] { '=', ':' };

        #endregion "Member Variables"

        protected override void ClearItems()
        {
            this.options.Clear();
        }

        protected override void InsertItem(int index, Option item)
        {
            Add(item);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            Option p = Items[index];
            foreach (string name in GetOptionNames(p.Prototypes))
            {
                this.options.Remove(name);
            }
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, Option item)
        {
            RemoveItem(index);
            Add(item);
            base.SetItem(index, item);
        }

        public new Options Add(Option option)
        {
            foreach (string name in GetOptionNames(option.Prototypes))
            {
                this.options.Add(name, option);
            }
            return this;
        }

        public Options Add(string options, Action<string> action)
        {
            return Add(options, null, action);
        }

        public Options Add(string options, string description, Action<string> action)
        {
            Option p = new Option(options, description, action);
            base.Add(p);
            return this;
        }

        public Options Add<T>(string options, Action<T> action)
        {
            return Add(options, null, action);
        }

        public Options Add<T>(string options, string description, Action<T> action)
        {
            TypeConverter c = TypeDescriptor.GetConverter(typeof(T));
            Action<string> a = delegate (string s)
            {
                action(s != null ? (T)c.ConvertFromString(s) : default(T));
            };
            return Add(options, description, a);
        }

        private static IEnumerable<string> GetOptionNames(string[] names)
        {
            foreach (string name in names)
            {
                int end = name.IndexOfAny(NameTerminator);
                if (end >= 0)
                    yield return name.Substring(0, end);
                else
                    yield return name;
            }
        }

        public IEnumerable<string> Parse(IEnumerable<string> options)
        {
            Option p = null;
            bool process = true;
            foreach (string option in options)
            {
                if (option == "--")
                {
                    process = false;
                    continue;
                }
                if (!process)
                {
                    yield return option;
                    continue;
                }
                Match m = ValueOption.Match(option);
                if (!m.Success)
                {
                    if (p != null)
                    {
                        p.Action(option);
                        p = null;
                    }
                    else
                        yield return option;
                }
                else
                {
                    string f = m.Groups["flag"].Value;
                    string n = m.Groups["name"].Value;
                    string v = !m.Groups["value"].Success
                        ? null
                        : m.Groups["value"].Value;
                    do
                    {
                        Option p2;
                        if (this.options.TryGetValue(n, out p2))
                        {
                            p = p2;
                            break;
                        }

                        // no match; is it a bool option?
                        if (n.Length >= 1 && (n[n.Length - 1] == '+' || n[n.Length - 1] == '-') &&
                                this.options.TryGetValue(n.Substring(0, n.Length - 1), out p2))
                        {
                            v = n[n.Length - 1] == '+' ? n : null;
                            p2.Action(v);
                            p = null;
                            break;
                        }

                        // is it a bundled option?
                        if (f == "-" && this.options.TryGetValue(n[0].ToString(), out p2))
                        {
                            int i = 0;
                            do
                            {
                                if (p2.OptionValue != OptionValue.None)
                                    throw new InvalidOperationException(string.Format("Unsupported using bundled option '{0}' that requires a value", n[i]));

                                p2.Action(n);
                            } while (++i < n.Length && this.options.TryGetValue(n[i].ToString(), out p2));
                        }

                        // not a know option; either a value for a previous option
                        if (p != null)
                        {
                            p.Action(option);
                            p = null;
                        }
                        // or a stand-alone argument
                        else
                            yield return option;
                    } while (false);
                    if (p != null)
                    {
                        switch (p.OptionValue)
                        {
                            case OptionValue.None:
                                p.Action(n);
                                p = null;
                                break;

                            case OptionValue.Optional:
                            case OptionValue.Required:
                                if (v != null)
                                {
                                    p.Action(v);
                                    p = null;
                                }
                                break;
                        }
                    }
                }
            }
            if (p != null)
            {
                NoValue(ref p, "");
            }
        }

        private static void NoValue(ref Option p, string option)
        {
            if (p != null && p.OptionValue == OptionValue.Optional)
            {
                p.Action(null);
                p = null;
            }
            else if (p != null && p.OptionValue == OptionValue.Required)
            {
                throw new InvalidOperationException("Expecting value after option " +
                    p.Prototype + ", found " + option);
            }
        }

        public void WriteOptionDescriptions(TextWriter o)
        {
            foreach (Option p in this)
            {
                List<string> names = new List<string>(GetOptionNames(p.Prototypes));

                int written = 0;
                if (names[0].Length == 1)
                {
                    Write(o, ref written, "  -");
                    Write(o, ref written, names[0]);
                }
                else
                {
                    Write(o, ref written, "      --");
                    Write(o, ref written, names[0]);
                }

                for (int i = 1; i < names.Count; ++i)
                {
                    Write(o, ref written, ", ");
                    Write(o, ref written, names[i].Length == 1 ? "-" : "--");
                    Write(o, ref written, names[i]);
                }

                if (p.OptionValue == OptionValue.Optional)
                    Write(o, ref written, "[=VALUE]");
                else if (p.OptionValue == OptionValue.Required)
                    Write(o, ref written, "=VALUE");

                if (written < optionWidth)
                    o.Write(new string(' ', optionWidth - written));
                else
                {
                    o.WriteLine();
                    o.Write(new string(' ', optionWidth));
                }

                o.WriteLine(p.Description);
            }
        }

        private static void Write(TextWriter o, ref int n, string s)
        {
            n += s.Length;
            o.Write(s);
        }
    }
}