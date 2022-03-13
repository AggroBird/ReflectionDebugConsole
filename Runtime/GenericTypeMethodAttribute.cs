// Copyright, 2021, AggrobirdGK

using System;

namespace AggroBird.DebugConsole
{
    // Special attribute for simulating generics.
    // When decorating a method with this attribute, if argIndex matches
    // the position of a parameter which is of type System.Type, the return
    // value will be casted to the provided type.
    // This allows for methods similar to FindObjectOfType<T>() where the 
    // return value is casted to T.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class GenericTypeMethodAttribute : Attribute
    {
        public GenericTypeMethodAttribute(int argIndex)
        {
            ArgIndex = argIndex;
        }

        public int ArgIndex { get; private set; }
    }
}