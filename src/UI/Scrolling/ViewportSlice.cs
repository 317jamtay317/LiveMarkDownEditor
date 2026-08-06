namespace UI.Scrolling;

/// <summary>
/// Where a span of the Visual Document sits in document coordinates: its top and bottom measured from
/// the start of the document rather than from the top of the Viewport, so it does not move as the view
/// scrolls (INV-074).
/// </summary>
/// <param name="Top">The span's top edge, in document coordinates.</param>
/// <param name="Bottom">The span's bottom edge, in document coordinates.</param>
public readonly record struct DocumentExtent(double Top, double Bottom);

/// <summary>
/// The contiguous run of a document-ordered set that a Viewport reaches — the members an overlay must
/// actually draw. Because the set is in document order the run is unbroken, so it is described by a
/// first index and a count rather than by a list (INV-074).
/// </summary>
/// <param name="First">The index of the first member on screen.</param>
/// <param name="Count">How many consecutive members the Viewport reaches.</param>
public readonly record struct ViewportSlice(int First, int Count)
{
    /// <summary>The slice that reaches nothing — what an off-screen or collapsed Viewport yields.</summary>
    public static ViewportSlice Empty => new(0, 0);

    /// <summary>Whether the Viewport reaches no member of the set at all.</summary>
    public bool IsEmpty => Count <= 0;

    /// <summary>The index of the last member on screen. Meaningless when the slice is empty.</summary>
    public int Last => First + Count - 1;
}

/// <summary>
/// A document-ordered set of <see cref="DocumentExtent"/>s prepared for slicing: built once per layout
/// and then asked, on every scroll, which of its members a Viewport reaches. Answering is two binary
/// searches, so a repaint's cost follows the Viewport rather than the length of the document (INV-074).
/// </summary>
/// <remarks>
/// The second search is what makes a tall span work. A Code Block taller than the Viewport has neither
/// edge on screen while the user reads its middle, so searching for spans whose own bottom is below the
/// Viewport top would drop it. The index therefore records, for each member, the furthest bottom
/// reached by it or by anything before it. That running maximum only ever grows, which keeps it binary
/// searchable, and the first member whose furthest bottom reaches the Viewport begins the slice.
/// <para>
/// The slice is deliberately one unbroken run rather than the exact set of intersecting members: a few
/// extra off-screen spans cost one cheap culled draw each, while hunting for scattered ones would cost
/// the walk this exists to avoid.
/// </para>
/// </remarks>
public sealed class ExtentIndex
{
    private static readonly ExtentIndex EmptyIndex = new([], [], []);

    private readonly DocumentExtent[] _extents;
    private readonly double[] _furthestBottoms;
    private readonly int[] _sourceIndexes;

    private ExtentIndex(DocumentExtent[] extents, double[] furthestBottoms, int[] sourceIndexes)
    {
        _extents = extents;
        _furthestBottoms = furthestBottoms;
        _sourceIndexes = sourceIndexes;
    }

    /// <summary>The index that holds nothing — what an overlay with no spans to draw slices against.</summary>
    public static ExtentIndex Empty => EmptyIndex;

    /// <summary>
    /// The run of a document-ordered set of spans that a Viewport reaches, where a position is anything
    /// that can be compared — a height, or a <c>TextPointer</c>, which compares without resolving any
    /// layout and so costs a repaint nothing.
    /// </summary>
    /// <typeparam name="TPosition">How a position in the document is expressed.</typeparam>
    /// <param name="starts">Each span's start, in document order.</param>
    /// <param name="furthestEnds">
    /// For each span, the furthest end reached by it or by any span before it — the running maximum that
    /// keeps a span taller than the Viewport in its own slice. Non-decreasing by construction.
    /// </param>
    /// <param name="viewportTop">The Viewport's top edge.</param>
    /// <param name="viewportBottom">The Viewport's bottom edge.</param>
    /// <param name="compare">Orders two positions, as <see cref="Comparison{T}"/> does.</param>
    /// <returns>The slice on screen, or <see cref="ViewportSlice.Empty"/> when the Viewport reaches nothing.</returns>
    public static ViewportSlice Of<TPosition>(
        IReadOnlyList<TPosition> starts,
        IReadOnlyList<TPosition> furthestEnds,
        TPosition viewportTop,
        TPosition viewportBottom,
        Comparison<TPosition> compare)
    {
        if (starts.Count == 0 || compare(viewportTop, viewportBottom) > 0)
        {
            return ViewportSlice.Empty;
        }

        // The last span beginning at or above the Viewport's bottom: everything after it is off screen.
        int low = 0, high = starts.Count - 1, last = -1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (compare(starts[middle], viewportBottom) <= 0)
            {
                last = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (last < 0)
        {
            return ViewportSlice.Empty;
        }

        // The first span that — itself or through one before it — reaches down to the Viewport's top.
        low = 0;
        high = furthestEnds.Count - 1;
        var first = furthestEnds.Count;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (compare(furthestEnds[middle], viewportTop) >= 0)
            {
                first = middle;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return first > last ? ViewportSlice.Empty : new ViewportSlice(first, last - first + 1);
    }

    /// <summary>How many spans the index holds.</summary>
    public int Count => _extents.Length;

    /// <summary>
    /// How many spans the most recent <see cref="Slice"/> inspected. Diagnostics for the tests that pin
    /// INV-074's cost guarantee; nothing in the app reads it.
    /// </summary>
    internal int ProbesOfLastSlice { get; private set; }

    /// <summary>
    /// Prepares <paramref name="extents"/> for slicing, ordering them by top edge and recording the
    /// running furthest bottom. Callers build their sets by walking the document, so the ordering is
    /// normally already the one they handed in.
    /// </summary>
    /// <param name="extents">The spans to index, in any order.</param>
    /// <returns>The prepared index.</returns>
    public static ExtentIndex Build(IReadOnlyList<DocumentExtent> extents)
    {
        if (extents.Count == 0)
        {
            return EmptyIndex;
        }

        var order = new int[extents.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        // A stable order by top edge: two spans starting on the same line keep the order they arrived in,
        // which is the order the document put them in.
        Array.Sort(order, (left, right) =>
        {
            var byTop = extents[left].Top.CompareTo(extents[right].Top);
            return byTop != 0 ? byTop : left.CompareTo(right);
        });

        var ordered = new DocumentExtent[extents.Count];
        var furthest = new double[extents.Count];
        var furthestSoFar = double.NegativeInfinity;
        for (var i = 0; i < order.Length; i++)
        {
            ordered[i] = extents[order[i]];
            furthestSoFar = Math.Max(furthestSoFar, ordered[i].Bottom);
            furthest[i] = furthestSoFar;
        }

        return new ExtentIndex(ordered, furthest, order);
    }

    /// <summary>The span at <paramref name="index"/> within the index's own ordering.</summary>
    /// <param name="index">A position in the index, such as a <see cref="ViewportSlice.First"/>.</param>
    /// <returns>The span's Document Extent.</returns>
    public DocumentExtent ExtentAt(int index) => _extents[index];

    /// <summary>
    /// The position, in the list handed to <see cref="Build"/>, of the span at <paramref name="index"/>
    /// within the index's ordering — so a caller can draw from its own list.
    /// </summary>
    /// <param name="index">A position in the index, such as a <see cref="ViewportSlice.First"/>.</param>
    /// <returns>That span's position in the original list.</returns>
    public int SourceIndexAt(int index) => _sourceIndexes[index];

    /// <summary>
    /// The run of spans a Viewport reaches, given the Viewport's edges in document coordinates.
    /// </summary>
    /// <param name="viewportTop">The Viewport's top edge, in document coordinates.</param>
    /// <param name="viewportBottom">The Viewport's bottom edge, in document coordinates.</param>
    /// <returns>
    /// The slice on screen, or <see cref="ViewportSlice.Empty"/> when the Viewport reaches nothing —
    /// including when it has collapsed to no height, which shows nothing rather than everything.
    /// </returns>
    public ViewportSlice Slice(double viewportTop, double viewportBottom)
    {
        ProbesOfLastSlice = 0;

        if (_extents.Length == 0 || viewportBottom <= viewportTop)
        {
            return ViewportSlice.Empty;
        }

        var last = LastStartingAtOrAbove(viewportBottom);
        if (last < 0)
        {
            return ViewportSlice.Empty;
        }

        var first = FirstReachingDownTo(viewportTop);
        if (first > last)
        {
            return ViewportSlice.Empty;
        }

        return new ViewportSlice(first, last - first + 1);
    }

    // The last span whose top edge is at or above the Viewport's bottom — everything after it begins
    // below the screen. Tops are non-decreasing, so this is a binary search.
    private int LastStartingAtOrAbove(double viewportBottom)
    {
        int low = 0, high = _extents.Length - 1, found = -1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            ProbesOfLastSlice++;
            if (_extents[middle].Top <= viewportBottom)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    // The first span that — itself or through something before it — reaches down to the Viewport's top.
    // The running furthest bottom only grows, so this is a binary search too.
    private int FirstReachingDownTo(double viewportTop)
    {
        int low = 0, high = _furthestBottoms.Length - 1, found = _furthestBottoms.Length;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            ProbesOfLastSlice++;
            if (_furthestBottoms[middle] >= viewportTop)
            {
                found = middle;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return found;
    }
}
