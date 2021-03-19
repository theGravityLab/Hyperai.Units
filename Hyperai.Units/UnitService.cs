using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Relations;
using Hyperai.Services;
using Hyperai.Units.Attributes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Hyperai.Units
{
    public class UnitService : IUnitService
    {
        private struct QueueEntry
        {
            public ActionDelegate Action;
            public TimeSpan Timeout;
            public DateTime CreatedAt;

            public QueueEntry(ActionDelegate action, TimeSpan timeout)
            {
                Action = action;
                Timeout = timeout;
                CreatedAt = DateTime.Now;
            }
        }

        private readonly Dictionary<Channel, ConcurrentQueue<QueueEntry>> invaders = new Dictionary<Channel, ConcurrentQueue<QueueEntry>>();
        private IEnumerable<ActionEntry> entries = null;

        private readonly IServiceProvider _provider;
        private readonly IMessageChainFormatter _formatter;
        private readonly IMessageChainParser _parser;
        private readonly ILogger<UnitService> _logger;

        public UnitService(IServiceProvider provider, IMessageChainFormatter formatter, IMessageChainParser parser, ILogger<UnitService> logger)
        {
            _provider = provider;
            _formatter = formatter;
            _logger = logger;
            _parser = parser;
        }

        public void Handle(MessageContext context)
        {
            bool flag = false;
            foreach (Channel channel in invaders.Keys)
            {
                if (channel.Match(context.User.Identity, context.Type == MessageEventType.Group ? (long?)context.Group.Identity : null))
                {
                    if (invaders[channel].TryDequeue(out QueueEntry action))
                    {
                        if (DateTime.Now < action.CreatedAt + action.Timeout)
                        {
                            action.Action(context);
                        }
                        else
                        {
                            MessageComponent[] chain = new MessageComponent[] { new Plain("❌你没能在时限内提供完整参数, 该操作已取消.") };
                            context.ReplyAsync(new MessageChain(chain)).Wait();
                        }
                        flag = true;
                    }
                    else
                    {
                        invaders.Remove(channel);
                    }
                    break;
                }
            }
            if (!flag)
            {
                IEnumerable<ActionEntry> ava = GetEntries().Where(x => x.Type == context.Type);
                foreach (ActionEntry e in ava)
                {
                    HandleOne(e, context);
                }
            }
        }

        public void HandleOne(ActionEntry entry, MessageContext context)
        {
            if (entry.State is int errorCount && errorCount >= 3)
            {
                if (errorCount == 3)
                {
                    _logger.LogWarning("An Action has met its error limit and has been disabled: " + entry);
                }

                return;
            }

            #region Extract Check

            ExtractAttribute extract = entry.Action.GetCustomAttribute<ExtractAttribute>();
            Dictionary<string, MessageChain> dict = new Dictionary<string, MessageChain>();

            if (extract != null)
            {
                string text = _formatter.Format(context.Message.AsReadable());
                if (extract.TrimSpaces)
                {
                    char[] rawChars = text.ToArray();
                    char[] output = new char[rawChars.Length];
                    int j = 0;
                    for (int i = 0; i < rawChars.Length; i++)
                    {
                        if ((j == 0 && rawChars[i] != ' ') || (j > 0 && (output[j - 1] != ' ' || rawChars[i] != ' ')))
                        {
                            output[j] = rawChars[i];
                            j++;
                        }
                    }

                    while (j > 0 && output[j - 1] == ' ')
                    {
                        j--;
                    }

                    text = new string(output[0..j]);
                }

                Match match = extract.Pattern.Match(text);
                if (match.Success)
                {
                    string[] names = extract.Names.ToArray();
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        dict.Add(names[i - 1], _parser.Parse(match.Groups[i].Value));
                    }
                }
                else
                {
                    return;
                }
            }

            #endregion Extract Check

            #region Filter Check

            object[] filterBys = entry.Action.GetCustomAttributes(typeof(FilterByAttribute), false);
            string failureMessage = null;
            bool pass = filterBys.All(x =>
            {
                FilterByAttribute filter = (FilterByAttribute)x;
                if (!filter.Filter.Check(context))
                {
                    failureMessage = filter.FailureMessage;
                    return false;
                }
                return true;
            });
            if (!pass)
            {
                if (failureMessage != null)
                {
                    MessageChain chain = new MessageChain(new MessageComponent[] { new Plain(entry.Action.Name + ": " + failureMessage) });
                    context.ReplyAsync(chain).Wait();
                }
                return;
            }

            #endregion Filter Check

            InvokeOne(entry, context, dict);
        }

        private void InvokeOne(ActionEntry entry, MessageContext context, Dictionary<string, MessageChain> names)
        {
            ParameterInfo[] paras = entry.Action.GetParameters();
            object[] paList = new object[paras.Length];
            try
            {
                foreach (ParameterInfo para in paras)
                {
                    if (names.ContainsKey(para.Name))
                    {
                        // pattern
                        paList[para.Position] = para.ParameterType switch
                        {
                            _ when para.ParameterType == typeof(string) => _formatter.Format(names[para.Name]),
                            _ when para.ParameterType == typeof(MessageChain) => names[para.Name],
                            _ when typeof(MessageComponent).IsAssignableFrom(para.ParameterType) => names[para.Name].FirstOrDefault(x => x.GetType() == para.ParameterType),
                            // _ when para.ParameterType == typeof(Member) && names[para.Name].Any(x
                            // => x is At) => GetMember(((At)names[para.Name].First(x => x is At)).TargetId),
                            // unit 不应该即时计算
                            _ when para.ParameterType != typeof(string) && para.ParameterType.IsValueType => typeof(Convert).GetMethod("To" + para.ParameterType.Name, new Type[] { typeof(string) }).Invoke(null, new object[] { _formatter.Format(names[para.Name]) }),
                            _ => throw new NotImplementedException("Pattern type not supported: " + para.ParameterType.FullName),
                        };
                    }
                    else
                    {
                        // context
                        paList[para.Position] = para.ParameterType switch
                        {
                            _ when para.ParameterType == typeof(string) => _formatter.Format(context.Message),
                            _ when para.ParameterType == typeof(Relations.Group) => context.Group,
                            _ when para.ParameterType == typeof(Self) => context.Me,
                            _ when para.ParameterType == typeof(MessageChain) => context.Message,
                            _ when para.ParameterType == typeof(DateTime) => context.SentAt,
                            _ when para.ParameterType == typeof(IApiClient) => context.Client,
                            _ when para.ParameterType == typeof(MessageEventType) => context.Type,
                            _ when para.ParameterType.IsAssignableFrom(context.User.GetType()) => context.User,
                            _ => throw new NotImplementedException("Context type not supported: " + para.ParameterType.FullName),
                        };
                    }
                }
            }
            catch (Exception)
            {
                if (entry.State is int cnt)
                {
                    entry.State = cnt + 1;
                }
                _logger.LogError("Failed to configure context of Unit Action.");
                return;
            }
            UnitBase unit = UnitFactory.Instance.CreateUnit(entry.Unit, context, _provider);
            _logger.LogInformation($"Action hit: {entry}");
            try
            {
                entry.Action.Invoke(unit, paList.ToArray());

                if (entry.State is int count)
                {
                    entry.State = count - 1;
                    if (count < 0)
                    {
                        entry.State = 0;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while executing Unit Action.");


                if (entry.State is int count)
                {
                    entry.State = count + 1;
                }
            }
        }

        public void WaitOne(Channel channel, ActionDelegate action, TimeSpan timeout)
        {
            if (!invaders.ContainsKey(channel))
            {
                invaders.Add(channel, new ConcurrentQueue<QueueEntry>());
            }
            invaders[channel].Enqueue(new QueueEntry(action, timeout));
        }

        public void SearchForUnits()
        {
            List<ActionEntry> ent = new List<ActionEntry>();
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies().Where(p => !p.IsDynamic))
            {
                IEnumerable<Type> types = ass.GetExportedTypes().Where(x => !x.IsAbstract && x.IsSubclassOf(typeof(UnitBase)));
                foreach (Type type in types)
                {
                    IEnumerable<MethodInfo> methods = type.GetMethods().Where(x => x.IsPublic && !x.IsStatic && !x.IsAbstract);
                    foreach (MethodInfo method in methods)
                    {
                        ReceiveAttribute att = method.GetCustomAttribute<ReceiveAttribute>();
                        if (att == null)
                        {
                            continue;
                        }

                        ActionEntry entry = new ActionEntry(att.Type, method, type, 0);
                        ent.Add(entry);
                    }
                }
            }
            entries = ent;
        }

        public IEnumerable<ActionEntry> GetEntries()
        {
            return entries;
        }
    }
}