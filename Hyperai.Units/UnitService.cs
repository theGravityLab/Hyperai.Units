using Hyperai.Events;
using Hyperai.Messages;
using Hyperai.Messages.ConcreteModels;
using Hyperai.Relations;
using Hyperai.Services;
using Hyperai.Units.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
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
        private readonly Dictionary<Channel, Queue<QueueEntry>> invaders = new Dictionary<Channel, Queue<QueueEntry>>();
        private IEnumerable<ActionEntry> entries = null;

        private readonly IServiceProvider _provider;
        private readonly IMessageChainFormatter _formatter;
        private readonly IMessageChainParser _parser;
        private readonly ILogger<UnitService> _logger;
        private readonly IApiClient _client;

        public UnitService(IServiceProvider provider, IMessageChainFormatter formatter, IMessageChainParser parser, ILogger<UnitService> logger, IApiClient client)
        {
            _provider = provider;
            _formatter = formatter;
            _logger = logger;
            _parser = parser;
            _client = client;
        }

        public void Handle(MessageContext context)
        {
            bool flag = false;
            foreach (Channel channel in invaders.Keys)
            {
                if (channel.Match(context.User.Identity, context.Type == MessageEventType.Group ? (long?)context.Group.Identity : null))
                {
                    if (invaders[channel].Count > 0)
                    {
                        QueueEntry action = invaders[channel].Dequeue();
                        if (DateTime.Now < action.CreatedAt + action.Timeout)
                        {
                            action.Action(context);
                        }
                    }
                    else
                    {
                        invaders.Remove(channel);
                    }
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
            string message = _formatter.Format(context.Message.AsReadable());
            ExtractAttribute extract = entry.Action.GetCustomAttribute<ExtractAttribute>();
            Dictionary<string, string> dict = new Dictionary<string, string>();

            if (extract != null)
            {
                Match match = extract.Pattern.Match(message);
                if (match.Success)
                {
                    string[] names = extract.Names.ToArray();
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        dict.Add(names[i - 1], match.Groups[i].Value);
                    }
                    if (CheckNames(dict, context, entry, extract.RawString))
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            #endregion

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
                    switch (context.Type)
                    {
                        case MessageEventType.Friend:
                            context.Client.SendFriendMessageAsync((Friend)context.User, chain).Wait();
                            break;
                        case MessageEventType.Group:
                            context.Client.SendGroupMessageAsync(context.Group, chain).Wait();
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
                return;
            }
            #endregion
            InvokeOne(entry, context, dict);
        }

        public void InvokeOne(ActionEntry entry, MessageContext context, Dictionary<string, string> names)
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
                            _ when para.ParameterType == typeof(string) => names[para.Name],
                            _ when para.ParameterType == typeof(MessageChain) => _parser.Parse(names[para.Name]),
                            _ when typeof(MessageComponent).IsAssignableFrom(para.ParameterType) => _parser.Parse(names[para.Name]).FirstOrDefault(x => x.GetType() == para.ParameterType),
                            _ when para.ParameterType != typeof(string) && para.ParameterType.IsValueType => typeof(Convert).GetMethod("To" + para.ParameterType.Name, new Type[] { typeof(string) }).Invoke(null, new object[] { names[para.Name] }),
                            _ => throw new NotImplementedException("Pattern type not supported: " + para.ParameterType.FullName),
                        };
                    }
                    else
                    {
                        // context
                        paList[para.Position] = para.ParameterType switch
                        {
                            _ when para.ParameterType == typeof(string) => _formatter.Format(context.Message.AsReadable()),
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
                    return;
                }
            }
            object unit = ActivatorUtilities.CreateInstance(_provider, entry.Unit);
            _logger.LogInformation($"Action hit: {entry}");
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

        public void WaitOne(Channel channel, ActionDelegate action, TimeSpan timeout)
        {
            if (!invaders.ContainsKey(channel))
            {
                invaders.Add(channel, new Queue<QueueEntry>());
            }
            invaders[channel].Enqueue(new QueueEntry(action, timeout));
        }

        public void WaitForInput(ActionEntry entry, MessageContext context, Dictionary<string, string> dict, string waitingName, string raw)
        {
            MessageChainBuilder builder = new MessageChainBuilder();
            builder.AddPlain($"请为以下参数提供值({raw}): \n{waitingName}");
            MessageChain chain = builder.Build();
            switch (context.Type)
            {
                case MessageEventType.Friend:
                    context.Client.SendFriendMessageAsync((Friend)context.User, chain).Wait();
                    WaitOne(new Channel() { UserId = context.User.Identity }, Delegate, TimeSpan.FromMinutes(1));
                    break;
                case MessageEventType.Group:
                    context.Client.SendGroupMessageAsync(context.Group, chain).Wait();
                    WaitOne(new Channel() { UserId = context.User.Identity, GroupId = context.Group.Identity }, Delegate, TimeSpan.FromMinutes(1));
                    break;
            }

            void Delegate(MessageContext message)
            {
                dict[waitingName] = _formatter.Format(message.Message.AsReadable());
                if (!CheckNames(dict, context, entry, raw))
                {
                    InvokeOne(entry, context, dict);
                }
            }
        }

        public bool CheckNames(Dictionary<string, string> names, MessageContext context, ActionEntry entry, string raw)
        {
            foreach (string key in names.Keys)
            {
                if (names[key] == "{}")
                {
                    WaitForInput(entry, context, names, key, raw);
                    return true;
                }
            }
            return false;
        }

        public void SearchForUnits()
        {
            List<ActionEntry> ent = new List<ActionEntry>();
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
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
