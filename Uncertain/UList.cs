using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.Uncertain
{
    public class UList<T> : Uncertain<T>, IList<Uncertain<T>>, IEnumerable<Uncertain<T>>
    {
        private readonly IList<Uncertain<T>> list = new List<Uncertain<T>>();


        public Uncertain<T> this[int index]
        {
            get
            {
                return list[index];
            }

            set
            {
                list[index] = value;
            }
        }

        public int Count
        {
            get
            {
                return list.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
               return  list.IsReadOnly;
            }
        }

        public override void Accept(IUncertainVisitor visitor)
        {
			visitor.Visit (this);
        }

        public void Add(Uncertain<T> item)
        {
            list.Add(item);
        }

        public void Clear()
        {
            list.Clear();
        }
		       

        public bool Contains(Uncertain<T> item)
        {
            return list.Contains(item);
        }

        public void CopyTo(Uncertain<T>[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Uncertain<T>> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public override IEnumerable<Weighted<T>> GetSupport()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(Uncertain<T> item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, Uncertain<T> item)
        {
            list.Insert(index, item);
        }


        public bool Remove(Uncertain<T> item)
        {
            throw new NotImplementedException();
        }

        
        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        IEnumerator<Uncertain<T>> IEnumerable<Uncertain<T>>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }
    }
}
