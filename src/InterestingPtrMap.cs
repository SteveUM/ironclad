using System;
using System.Collections.Generic;
using System.Threading;

using Ironclad.Structs;

namespace Ironclad
{
    
    public delegate void PtrFunc(IntPtr ptr);
    
    public class InterestingPtrMap
    {
        private Dictionary<object, IntPtr> obj2ptr = new Dictionary<object, IntPtr>();
        private Dictionary<IntPtr, object> ptr2obj = new Dictionary<IntPtr, object>();
        
        private Dictionary<WeakReference, IntPtr> ref2ptr = new Dictionary<WeakReference, IntPtr>();
        private Dictionary<IntPtr, WeakReference> ptr2ref = new Dictionary<IntPtr, WeakReference>();
        private StupidSet strongrefs = new StupidSet();
        
        private Object _lock = new Object();
    
        public void Associate(IntPtr ptr, object obj)
        {
            lock (this._lock)
            {
                this.ptr2obj[ptr] = obj;
                this.obj2ptr[obj] = ptr;
            }
        }
        
        public void Associate(IntPtr ptr, UnmanagedDataMarker udm)
        {
            lock (this._lock)
            {
                this.ptr2obj[ptr] = udm;
            }
        }
        
        public void BridgeAssociate(IntPtr ptr, object obj)
        {
            lock (this._lock)
            {
                WeakReference wref = new WeakReference(obj, true);
                this.ptr2ref[ptr] = wref;
                this.ref2ptr[wref] = ptr;
                this.strongrefs.Add(obj);
            }
        }
        
        private void UpdateStrength(IntPtr ptr, object obj)
        {
            lock (this._lock)
            {
                int refcnt = CPyMarshal.ReadIntField(ptr, typeof(PyObject), "ob_refcnt");
                if (refcnt > 1)
                {
                    this.Strengthen(obj);
                }
                else
                {
                    this.Weaken(obj);
                }
            }
        }
        
        public void UpdateStrength(IntPtr ptr)
        {
            lock (this._lock)
            {
                if (this.ptr2obj.ContainsKey(ptr))
                {
                    // items in this mapping are always strongly referenced
                    return;
                }
                this.UpdateStrength(ptr, this.GetObj(ptr));
            }
        }
        
        public void CheckBridgePtrs()
        {
            lock (this._lock)
            {
                foreach (KeyValuePair<WeakReference, IntPtr> pair in this.ref2ptr)
                {
                    this.UpdateStrength(pair.Value, pair.Key.Target);
                }
            }
        }
        
        public void MapOverBridgePtrs(PtrFunc f)
        {
            lock (this._lock)
            {
                Dictionary<IntPtr, WeakReference>.KeyCollection keys = this.ptr2ref.Keys;
                IntPtr[] keysCopy = new IntPtr[keys.Count];
                keys.CopyTo(keysCopy, 0);
                foreach (IntPtr ptr in keysCopy)
                {
                    f(ptr);
                }
            }
        }
        
        public void Strengthen(object obj)
        {
            lock (this._lock)
            {
                this.strongrefs.Add(obj);
            }
        }
        
        public void Weaken(object obj)
        {
            lock (this._lock)
            {
                this.strongrefs.RemoveIfPresent(obj);
            }
        }
        
        public void Release(IntPtr ptr)
        {
            lock (this._lock)
            {
                if (this.ptr2obj.ContainsKey(ptr))
                {
                    object obj = this.ptr2obj[ptr];
                    this.ptr2obj.Remove(ptr);
                    if (this.obj2ptr.ContainsKey(obj))
                    {
                        this.obj2ptr.Remove(obj);
                    }
                }
                else if (this.ptr2ref.ContainsKey(ptr))
                {
                    WeakReference wref = this.ptr2ref[ptr];
                    this.ptr2ref.Remove(ptr);
                    this.ref2ptr.Remove(wref);
                    if (wref.IsAlive)
                    {
                        this.strongrefs.RemoveIfPresent(wref.Target);
                    }
                }
                else
                {
                    throw new KeyNotFoundException(String.Format("tried to release unmapped ptr {0}", ptr));
                }
            }
        }
        
        public bool HasObj(object obj)
        {
            lock (this._lock)
            {
                if (this.obj2ptr.ContainsKey(obj))
                {
                    return true;
                }
                foreach (WeakReference wref in this.ref2ptr.Keys)
                {
                    if (Object.ReferenceEquals(obj, wref.Target))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        
        public IntPtr GetPtr(object obj)
        {
            lock (this._lock)
            {
                if (this.obj2ptr.ContainsKey(obj))
                {
                    return this.obj2ptr[obj];
                }
                foreach (WeakReference wref in this.ref2ptr.Keys)
                {
                    if (Object.ReferenceEquals(obj, wref.Target))
                    {
                        return this.ref2ptr[wref];
                    }
                }
                throw new KeyNotFoundException(String.Format("No obj-to-ptr mapping for {0}", obj));
            }
        }
        
        public bool HasPtr(IntPtr ptr)
        {
            lock (this._lock)
            {
                if (this.ptr2obj.ContainsKey(ptr))
                {
                    return true;
                }
                if (this.ptr2ref.ContainsKey(ptr))
                {
                    return true;
                }
                return false;
            }
        }
        
        public object GetObj(IntPtr ptr)
        {
            lock (this._lock)
            {
                if (this.ptr2obj.ContainsKey(ptr))
                {
                    return this.ptr2obj[ptr];
                }
                if (this.ptr2ref.ContainsKey(ptr))
                {
                    WeakReference wref = this.ptr2ref[ptr];
                    if (wref.IsAlive)
                    {
                        return wref.Target;
                    }
                    throw new NullReferenceException(String.Format("Weakly mapped object for ptr {0} was apparently GCed too soon", ptr));
                }
                throw new KeyNotFoundException(String.Format("No ptr-to-obj mapping for {0}", ptr));
            }
        }
    
    }
    

}

