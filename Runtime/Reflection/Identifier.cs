// Copyright, AggrobirdGK

#if (INCLUDE_DEBUG_CONSOLE || UNITY_EDITOR) && !EXCLUDE_DEBUG_CONSOLE

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace AggroBird.Reflection
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

                    // Skip nested (member scanner will pick it up)
                    if (type.IsNested) continue;

                    // Skip hidden
                    if (safeMode && !type.IsPublic) continue;

                    ArrayView<StringView> name = SplitName(type.FullName, buffer);
                    if (name.Length == 0) continue;

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

        private Identifier(StringView name)
        {
            Name = name.ToString();
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
                    case '`':
                        buffer.Add(fullName.SubView(index, i - index));
                        return buffer.ToArray();
                }
            }
            buffer.Add(fullName.SubView(index, fullName.Length - index));
            return buffer.ToArray();
        }


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
            for (int i = 0; i < name.Length; i++)
            {
                if (!current.TryFindIdentifier(name[i], out Identifier next))
                {
                    next = new Identifier(name[i]);
                    current.AddChild(next);
                }
                current = next;
            }

            if (current.types == null) current.types = new List<Type>();
            current.types.Add(type);
        }

        private void AddChild(Identifier child)
        {
            if (children == null) children = new Dictionary<StringView, Identifier>();
            children.Add(child.Name, child);
        }


        private Dictionary<StringView, Identifier> children = null;
        private List<Type> types = null;

        public int ChildCount => children == null ? 0 : children.Count;
        public bool IsNamespace => types == null || types.Count == 0;

        public IReadOnlyCollection<Identifier> Children => children == null ? (IReadOnlyCollection<Identifier>)Array.Empty<Identifier>() : (IReadOnlyCollection<Identifier>)children.Values;
        public IReadOnlyList<Type> Types => types == null ? (IReadOnlyList<Type>)Array.Empty<Type>() : (IReadOnlyList<Type>)types;

        public string Name { get; private set; }
        public override string ToString() => Name;
    }
}
#endif
