using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using IronPython.Runtime;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Ironclad.Structs;


namespace Ironclad
{

    public partial class Python25Mapper
    {
        public override void
        Fill_PyBaseObject_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_init", this.GetAddress("PyBaseObject_Init"));
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_alloc", this.GetAddress("PyType_GenericAlloc"));
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_new", this.GetAddress("PyType_GenericNew"));
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_dealloc", this.GetAddress("PyBaseObject_Dealloc"));
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_free", this.GetAddress("PyObject_Free"));
            this.map.Associate(address, TypeCache.Object);
        }
        
        public override void
        Fill_PyType_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.PythonType);
        }

        public override void 
        Fill_PyFile_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.PythonFile);
        }

        public override void
        Fill_PyInt_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.Int32);
        }

        public override void
        Fill_PyLong_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.BigInteger);
        }

        public override void
        Fill_PyFloat_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.Double);
        }

        public override void
        Fill_PyComplex_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.Complex64);
        }
        
        public override void 
        Fill_PyCObject_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_dealloc", this.GetAddress("PyCObject_Dealloc"));
            this.map.Associate(address, typeof(OpaquePyCObject));
        }
        
        public override void
        Fill_PyDict_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.Dict);
        }
        
        public override void
        Fill_PyList_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_dealloc", this.GetAddress("PyList_Dealloc"));
            this.map.Associate(address, TypeCache.List);
        }
        
        public override void
        Fill_PyString_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            this.map.Associate(address, TypeCache.String);
        }
        
        public override void
        Fill_PyTuple_Type(IntPtr address)
        {
            CPyMarshal.WriteIntField(address, typeof(PyTypeObject), "ob_refcnt", 1);
            CPyMarshal.WritePtrField(address, typeof(PyTypeObject), "tp_dealloc", this.GetAddress("PyTuple_Dealloc"));
            this.map.Associate(address, TypeCache.PythonTuple);
        }
        
        
        public void
        ReadyBuiltinTypes()
        {
            this.PyType_Ready(this.PyType_Type);
            this.PyType_Ready(this.PyBaseObject_Type);
            this.PyType_Ready(this.PyInt_Type);
            this.PyType_Ready(this.PyLong_Type);
            this.PyType_Ready(this.PyFloat_Type);
            this.PyType_Ready(this.PyComplex_Type);
            this.PyType_Ready(this.PyString_Type);
            this.PyType_Ready(this.PyTuple_Type);
            this.PyType_Ready(this.PyList_Type);
            this.PyType_Ready(this.PyDict_Type);
            this.PyType_Ready(this.PyFile_Type);
            this.PyType_Ready(this.PyCObject_Type);
        }
        
        
        public override int
        PyType_IsSubtype(IntPtr subtypePtr, IntPtr typePtr)
        {
            PythonType _type = this.Retrieve(typePtr) as PythonType;
            PythonType subtype = this.Retrieve(subtypePtr) as PythonType;
            if (subtype == null || _type == null) { return 0; }

            PythonType midtype;
            while (true)
            {
                midtype = (PythonType)PythonCalls.Call(Builtin.type, new object[1] { subtype });
                if (midtype == _type) { return 1; }
                if (subtype == midtype) { return 0; }
                subtype = midtype;
            }
        }
        
                
        private void InheritPtrField(IntPtr typePtr, string name)
        {
            IntPtr fieldPtr = CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), name);
            if (fieldPtr == IntPtr.Zero)
            {
                IntPtr basePtr = CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), "tp_base");
                if (basePtr != IntPtr.Zero)
                {
                    CPyMarshal.WritePtrField(typePtr, typeof(PyTypeObject), name,
                        CPyMarshal.ReadPtrField(basePtr, typeof(PyTypeObject), name));
                }
            }
        }
        
        
        public override int
        PyType_Ready(IntPtr typePtr)
        {
            if (typePtr == IntPtr.Zero)
            {
                return -1;
            }
            
            Py_TPFLAGS flags = (Py_TPFLAGS)CPyMarshal.ReadIntField(typePtr, typeof(PyTypeObject), "tp_flags");
            if ((Int32)(flags & Py_TPFLAGS.READY) != 0)
            {
                return 0;
            }
            
            IntPtr typeTypePtr = CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), "ob_type");
            if ((typeTypePtr == IntPtr.Zero) && (typePtr != this.PyType_Type))
            {
                CPyMarshal.WritePtrField(typePtr, typeof(PyTypeObject), "ob_type", this.PyType_Type);
            }
            IntPtr typeBasePtr = CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), "tp_base");
            if ((typeBasePtr == IntPtr.Zero) && (typePtr != this.PyBaseObject_Type))
            {
                typeBasePtr = this.PyBaseObject_Type;
                CPyMarshal.WritePtrField(typePtr, typeof(PyTypeObject), "tp_base", typeBasePtr);
            }
            PyType_Ready(typeBasePtr);
            
            this.InheritPtrField(typePtr, "tp_alloc");
            this.InheritPtrField(typePtr, "tp_init");
            this.InheritPtrField(typePtr, "tp_new");
            this.InheritPtrField(typePtr, "tp_dealloc");
            this.InheritPtrField(typePtr, "tp_free");
            this.InheritPtrField(typePtr, "tp_print");
            this.InheritPtrField(typePtr, "tp_repr");
            this.InheritPtrField(typePtr, "tp_str");
            this.InheritPtrField(typePtr, "tp_doc");
            this.InheritPtrField(typePtr, "tp_call");
            this.InheritPtrField(typePtr, "tp_as_number");
            
            flags |= Py_TPFLAGS.READY;
            CPyMarshal.WriteIntField(typePtr, typeof(PyTypeObject), "tp_flags", (Int32)flags);
            return 0;
        }
        
        public override IntPtr 
        PyType_GenericNew(IntPtr typePtr, IntPtr args, IntPtr kwargs)
        {
            PyType_GenericAlloc_Delegate dgt = (PyType_GenericAlloc_Delegate)CPyMarshal.ReadFunctionPtrField(
                typePtr, typeof(PyTypeObject), "tp_alloc", typeof(PyType_GenericAlloc_Delegate));
            return dgt(typePtr, 0);
        }
        
        public override IntPtr 
        PyType_GenericAlloc(IntPtr typePtr, int nItems)
        {
            int size = CPyMarshal.ReadIntField(typePtr, typeof(PyTypeObject), "tp_basicsize");
            
            if (nItems > 0)
            {
                int itemsize = CPyMarshal.ReadIntField(typePtr, typeof(PyTypeObject), "tp_itemsize");
                size += (nItems * itemsize);
            }
            
            IntPtr newInstance = this.allocator.Alloc(size);
            CPyMarshal.Zero(newInstance, size);
            CPyMarshal.WriteIntField(newInstance, typeof(PyObject), "ob_refcnt", 1);
            CPyMarshal.WritePtrField(newInstance, typeof(PyObject), "ob_type", typePtr);
            
            return newInstance;
        }
    }
}
