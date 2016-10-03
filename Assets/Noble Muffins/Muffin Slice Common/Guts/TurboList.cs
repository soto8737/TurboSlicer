using UnityEngine;

namespace NobleMuffins.MuffinSlicer {

	//WARNING!!!!!1111oneoneone
	
	//Inside here is is a class called TurboList. Do NOT use it.
	
	//It's an unsafe white-box class that is part of the TurboSlice black-box. The
	//differences between it and the .NET List are esoteric, specific and not relevant
	//to your needs.
	
	//Do not, under any circumstances, see it as a faster List for general use.
	//Read on only if you are studying or modifying TurboSlice.
	
	/* This is called a "TurboList" and it may seem useless as first,
	 * but profiling suggested it and there's a reason it's faster than the .NET List class.
	 * 
	 * Shea's Law states, "The ability to improve a design occurs primarily at the interfaces.
	 *  This is also the prime location for screwing it up."
	 * 
	 * This class provides nice examples of both.
	 * 
	 * List.AddRange was eating up a large chunk of time according to the profiler. This method only
	 * accepts IEnumerable. While this is good in its use case, it doesn't have access to the given
	 * set's size and discovering its size creates a lot of unnecessary work. Therefore, the first
	 * special feature of TurboList is that its interface lets it observe a given set's size.
	 * 
	 * The second is more dangerous; its model is directly exposed. Another chunk of time spent was getting
	 * at the data, copying it and sometimes simply getting an array from the List.
	 * 
	 * Do not use this class for anything else and do not assume that this will make anything else faster.
	 * It was designed to meet a particular use case - the Muffin Slicer's - and is a private subset of that class
	 * for a reason.
	 */
	public class TurboList<T> {
		private T[] content;
		private int capacity = 0;
		private int nextFigure = 0;
		
		public int Count {
			get {
				return nextFigure;
			}
			set {
				nextFigure = value;
			}
		}
		
		public T[] array {
			get {
				return content;
			}
		}
		
		public T[] ToArray()
		{
			T[] a = new T[nextFigure];
			System.Array.Copy(content, a, nextFigure);
			return a;
		}

		public void Clear() {
			Count = 0;
		}
		
		public TurboList(T[] copySource)
		{
			capacity = copySource.Length;
			content = new T[copySource.Length];
			System.Array.Copy(copySource, content, capacity);
			nextFigure = 0;
		}
		
		public TurboList(int _capacity)
		{
			capacity = _capacity;
			content = new T[capacity];
			nextFigure = 0;
		}
		
		public void EnsureCapacity(int i)
		{
			bool mustExpand = i > capacity;
			
			if(mustExpand)
			{
				System.Array.Resize<T>(ref content, i);
				capacity = i;
			}
		}
		
		public void AddArray(T[] source)
		{
			bool mustExpand = source.Length + nextFigure > capacity;
			
			if(mustExpand)
			{
				int capacity2 = (capacity * 3) / 2 + source.Length;
				T[] content2 = new T[capacity2];
				System.Array.Copy(content, content2, capacity);
				content = content2;
				capacity = capacity2;
			}
			
			System.Array.Copy(source, 0, content, nextFigure, source.Length);
			
			nextFigure += source.Length;
		}
		
		public T this[int i]
		{
			get {
				return content[i];
			}
		}
	}
	
}