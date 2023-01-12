// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace AggroBird.DebugConsole
{
    internal class Identifier
    {
        protected Identifier()
        {

        }
        public Identifier(Assembly[] assemblies, IReadOnlyList<string>[] usingNamespaces, bool safeMode, CancellationToken cancellationToken)
        {
            List<StringView> buffer = new List<StringView>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (!Expression.IncludeMember(type)) continue;

                    ArrayView<StringView> name = SplitName(type.FullName, buffer);
                    if (name.Length == 0) return;

                    // Skip hidden
                    if (safeMode && !type.IsPublic) continue;

                    AddTypeRecursive(name, type);
                    for (int i = 0; i < usingNamespaces.Length; i++)
                    {
                        IReadOnlyList<string> usingNamespace = usingNamespaces[i];
                        if (name.Length > usingNamespace.Count)
                        {
                            for (int j = 0; j < usingNamespace.Count; j++)
                            {
                                if (usingNamespace[j] != name[j])
                                {
                                    goto Skip;
                                }
                            }
                            AddTypeRecursive(name.SubView(usingNamespace.Count), type);
                        }
                    Skip:
                        continue;
                    }
                }
            }
        }

        private static ArrayView<StringView> SplitName(string fullName, List<StringView> buffer)
        {
            buffer.Clear();
            int index = 0;
            for (int i = 0; i < fullName.Length; i++)
            {
                switch (fullName[i])
                {
                    case '.':
                    case '+':
                        buffer.Add(fullName.SubView(index, i - index));
                        index = i + 1;
                        break;
                }
            }
            StringView last = fullName.SubView(index, fullName.Length - index);
            if (buffer.Count == 0)
            {
                singleItem[0] = last;
                return singleItem;
            }
            else
            {
                buffer.Add(last);
                return buffer.ToArray();
            }
        }
        private static readonly StringView[] singleItem = new StringView[1];


        public bool TryFindIdentifier(StringView name, out Identifier identifier)
        {
            if (children != null)
            {
                return children.TryGetValue(name, out identifier);
            }
            identifier = null;
            return false;
        }


        private void AddTypeRecursive(ArrayView<StringView> name, Type type)
        {
            Identifier current = this;
            for (int i = 0; i < name.Length - 1; i++)
            {
                if (!current.TryFindIdentifier(name[i], out Identifier next))
                {
                    next = new NamespaceIdentifier(name[i]);
                    current.AddChild(next);
                }
                current = next;
            }

            StringView last = name[name.Length - 1];
            if (current.TryFindIdentifier(last, out Identifier identifier))
            {
                if (identifier is NamespaceIdentifier)
                {
                    current.children[last] = new TypeIdentifier(type);
                }
                return;
            }
            else
            {
                current.AddChild(new TypeIdentifier(type));
            }
        }

        private void AddChild(Identifier child)
        {
            if (children == null) children = new Dictionary<StringView, Identifier>();
            children.Add(child.Name, child);
        }


        private Dictionary<StringView, Identifier> children = null;
        public int ChildCount => children == null ? 0 : children.Count;
        public IReadOnlyDictionary<StringView, Identifier> Children => children;

        public virtual string Name => string.Empty;
        public override string ToString() => Name;
    }

    internal class NamespaceIdentifier : Identifier
    {
        public NamespaceIdentifier(StringView name)
        {
            this.name = name.ToString();
        }


        private readonly string name;

        public override string Name => name;
    }

    internal sealed class TypeIdentifier : Identifier
    {
        public TypeIdentifier(Type type)
        {
            this.type = type;
        }


        public readonly Type type;

        public override string Name => type.Name;
    }
}
#endif