using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Fixture.Root
{
    /// <summary>Represents a marker contract.</summary>
    public interface IMarker
    {
    }

    /// <summary>Provides a visible base type.</summary>
    public abstract class BaseType
    {
    }

    /// <summary>Represents processing states.</summary>
    public enum SampleState : short
    {
        /// <summary>No state.</summary>
        None = 0,

        /// <summary>Started state.</summary>
        /// <remarks>Used while processing is active.</remarks>
        Started = 1,

        /// <summary>Finished state.</summary>
        Finished = 2,
    }

    /// <summary>Transforms input values.</summary>
    /// <typeparam name="TInput">Delegate input type.</typeparam>
    /// <typeparam name="TOutput">Delegate output type.</typeparam>
    public delegate TOutput Transformer<in TInput, out TOutput>(TInput input);

    /// <summary>Represents a byref-like buffer slice.</summary>
    public readonly ref struct BufferSlice
    {
        /// <summary>Gets the slice length.</summary>
        public readonly int Length;

        /// <summary>Initializes a new buffer slice.</summary>
        /// <param name="length">Slice length.</param>
        public BufferSlice(int length) =>
            Length = length;
    }

    /// <summary>Represents a nominal record.</summary>
    public record NameRecord(string Name);

    /// <summary>Represents a value record.</summary>
    public readonly record struct ValueRecord(int Value);

    /// <summary>Provides native interop members.</summary>
    public static class NativeMethods
    {
        /// <summary>Calls the native message beep API.</summary>
        /// <param name="type">Beep type.</param>
        /// <returns>Returns whether the API succeeded.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern bool MessageBeep(uint type);
    }

    /// <summary>Represents a constrained container.</summary>
    /// <typeparam name="TValue">Constrained value type.</typeparam>
    public class ConstrainedContainer<TValue>
        where TValue : BaseType, IMarker, new()
    {
    }

    /// <summary>Contains nullable reference type members.</summary>
    public class NullableContainer
    {
        /// <summary>Gets or sets the optional name.</summary>
        public string? OptionalName { get; set; }

        /// <summary>Creates a nullable map.</summary>
        /// <param name="prefix">Optional prefix.</param>
        /// <param name="values">Optional values.</param>
        /// <returns>Optional map.</returns>
        public Dictionary<string, string?>? CreateNullableMap(
            string? prefix,
            List<string?>? values) =>
            null;

        /// <summary>Returns text with a nullable contract.</summary>
        /// <param name="value">Input value.</param>
        /// <returns>Returned value.</returns>
        [return: MaybeNull]
        public string ReturnMaybeNull(string value) =>
            value;
    }

    /// <summary>Represents an outer generic type.</summary>
    /// <typeparam name="TOuter">Outer type parameter.</typeparam>
    public class Outer<TOuter>
        where TOuter : notnull
    {
        /// <summary>Represents a nested generic type.</summary>
        /// <typeparam name="TInner">Inner type parameter.</typeparam>
        public class Inner<TInner>
        {
            /// <summary>Combines values.</summary>
            /// <param name="key">Key parameter.</param>
            /// <param name="values">Values parameter.</param>
            /// <returns>Combined dictionary.</returns>
            public Dictionary<TOuter, TInner[]> Combine(TOuter key, TInner[] values) =>
                new()
                {
                    [key] = values,
                };
        }
    }

    /// <summary>Represents a generic sample type.</summary>
    /// <typeparam name="TItem">Primary item type.</typeparam>
    /// <typeparam name="TValue">Secondary value type.</typeparam>
    /// <remarks>Type remarks with <see cref="VisibilityContainer" /> and <c>inline code</c>.</remarks>
    /// <example>
    /// <code>
    /// var sample = new GenericSample&lt;int, string&gt;();
    /// </code>
    /// </example>
    /// <seealso cref="VisibilityContainer" />
    [Serializable]
    public class GenericSample<TItem, TValue> : BaseType, IMarker
    {
        /// <summary>Initializes a new instance.</summary>
        public GenericSample()
        {
        }

        /// <summary>Gets a shared value.</summary>
        public static readonly string SharedField = "shared";

        /// <summary>Gets the initial state.</summary>
        public const SampleState InitialState = SampleState.Started;

        /// <summary>Represents mutable data.</summary>
        /// <remarks>Legacy field.</remarks>
        [Obsolete("Use SharedField instead.")]
        public int MutableField;

        /// <summary>Gets or sets the current name.</summary>
        /// <value>Name property value.</value>
        public string Name { get; protected set; } = string.Empty;

        /// <summary>Gets or sets an indexed item.</summary>
        /// <param name="index">Indexer index.</param>
        /// <value>Indexed value.</value>
        public TValue this[int index]
        {
            get => default!;
            protected set
            {
            }
        }

        /// <summary>Raised when the sample changes.</summary>
        public event EventHandler? Changed;

        /// <summary>Formats a value.</summary>
        /// <param name="prefix">Prefix parameter.</param>
        /// <param name="level">Level parameter.</param>
        /// <returns>Formatted text.</returns>
        public string Format(string prefix = "prefix", int level = 5) => $"{prefix}:{level}";

        /// <summary>Transforms the supplied data.</summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="item">Item parameter.</param>
        /// <param name="values">Values parameter.</param>
        /// <param name="map">Map parameter.</param>
        /// <param name="buffer">Buffer parameter.</param>
        /// <param name="count">Count parameter.</param>
        /// <param name="aliases">Aliases parameter.</param>
        /// <returns>Transformation result.</returns>
        /// <remarks>Method remarks.</remarks>
        /// <example>
        /// <code>
        /// var count = 0;
        /// _ = sample.Transform&lt;string&gt;(default!, new List&lt;string[]&gt;(), null, ref count, out count, "alias");
        /// </code>
        /// </example>
        /// <seealso cref="Name" />
        public TResult Transform<TResult>(
            TItem item,
            List<TValue[]> values,
            Dictionary<string, TItem>? map,
            ref int buffer,
            out int count,
            params string[] aliases)
        {
            count = aliases.Length + buffer + (map?.Count ?? 0);
            return default!;
        }

        /// <summary>Consumes references.</summary>
        /// <param name="item">Item parameter.</param>
        /// <param name="value">Value parameter.</param>
        /// <param name="counter">Counter parameter.</param>
        public void ConsumeReferences(in TItem item, out TValue value, ref int counter)
        {
            value = default!;
            counter++;
        }

        /// <summary>Rewrites a map.</summary>
        /// <param name="map">Map parameter.</param>
        public void RewriteMap(ref Dictionary<string, TItem> map)
        {
        }

        /// <summary>Creates a constrained result.</summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <returns>Created result.</returns>
        public TResult CreateConstrained<TResult>()
            where TResult : BaseType, IMarker, new() =>
            new();

        /// <summary>Handles a matrix.</summary>
        /// <param name="matrix">Matrix parameter.</param>
        public void HandleMatrix(int[,] matrix)
        {
        }

        /// <summary>Uses a pointer.</summary>
        /// <param name="pointer">Pointer parameter.</param>
        public unsafe void UsePointer(int* pointer)
        {
        }

        /// <summary>Overload without parameters.</summary>
        public void Overload()
        {
        }

        /// <summary>Overload with one parameter.</summary>
        /// <param name="value">Value parameter.</param>
        public void Overload(int value)
        {
        }

        /// <summary>Converts a sample to a string.</summary>
        /// <param name="sample">Sample parameter.</param>
        public static implicit operator string(GenericSample<TItem, TValue> sample) => sample.Name;

        /// <summary>Raises the changed event.</summary>
        protected virtual void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

        internal void HiddenMethod()
        {
        }
    }

    /// <summary>Contains members with different visibility.</summary>
    public class VisibilityContainer
    {
        /// <summary>Visible field.</summary>
        public int VisibleField;

        /// <summary>Protected field.</summary>
        protected int ProtectedField;

        /// <summary>Hidden field.</summary>
        internal int HiddenField = 1;

        /// <summary>Hidden by editor browsable.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int HiddenByEditorBrowsableField;

        /// <summary>Visible property.</summary>
        public int VisibleProperty { get; set; }

        /// <summary>Protected property.</summary>
        public int ProtectedProperty
        {
            protected get;
            set;
        }

        /// <summary>Hidden property.</summary>
        internal int HiddenProperty { get; set; }

        /// <summary>Visible event.</summary>
        public event EventHandler? VisibleEvent;

        /// <summary>Hidden event.</summary>
        internal event EventHandler? HiddenEvent;

        /// <summary>Visible method.</summary>
        [EditorBrowsable(EditorBrowsableState.Always)]
        public void VisibleMethod()
        {
        }

        /// <summary>Protected method.</summary>
        protected void ProtectedMethod()
        {
        }

        /// <summary>Hidden method.</summary>
        internal void HiddenMethod()
        {
        }

        /// <summary>Raises the visible event.</summary>
        protected virtual void OnVisibleEvent() => VisibleEvent?.Invoke(this, EventArgs.Empty);

        internal void OnHiddenEvent() => HiddenEvent?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Provides extension members.</summary>
    public static class GenericSampleExtensions
    {
        /// <summary>Extends a sample.</summary>
        /// <typeparam name="TItem">Primary item type.</typeparam>
        /// <typeparam name="TValue">Secondary value type.</typeparam>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="sample">Sample parameter.</param>
        /// <param name="value">Value parameter.</param>
        /// <returns>Returned value.</returns>
        public static TResult Extend<TItem, TValue, TResult>(
            this GenericSample<TItem, TValue> sample,
            TResult value) => value;
    }
}

namespace Fixture.Secondary
{
    /// <summary>Represents another namespace type.</summary>
    public class SecondaryType
    {
        /// <summary>Returns text.</summary>
        /// <returns>Static text.</returns>
        public string Echo() => "secondary";
    }
}
