using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Calls;
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;

using Ironclad.Structs;

using DispatchTable = System.Collections.Generic.Dictionary<string, System.Delegate>;

namespace Ironclad
{
    public partial class Python25Mapper : PythonMapper
    {

        private void
        ExecInModule(string code, PythonModule module)
        {
            ScriptSource script = this.engine.CreateScriptSourceFromString(code, SourceCodeKind.Statements);
            ScriptScope scope = this.GetModuleScriptScope(module);
            script.Execute(scope);
        }

        private ScriptScope
        GetModuleScriptScope(PythonModule module)
        {
            return this.engine.CreateScope(module.Scope.Dict);
        }


        private PythonContext
        GetPythonContext()
        {
            FieldInfo _languageField = (FieldInfo)(this.engine.GetType().GetMember(
                "_language", BindingFlags.NonPublic | BindingFlags.Instance)[0]);
            PythonContext ctx = (PythonContext)_languageField.GetValue(this.engine);
            return ctx;
        }
        
        
        public override IntPtr 
        Py_InitModule4(string name, IntPtr methods, string doc, IntPtr self, int apiver)
        {
            Dictionary<string, object> globals = new Dictionary<string, object>();
            DispatchTable methodTable = new DispatchTable();

            globals["__doc__"] = doc;
            globals["_dispatcher"] = PythonCalls.Call(this.dispatcherClass, new object[] { this, methodTable });

            // hack to help moduleCode run -- can't import from System for some reason
            globals["nullPtr"] = IntPtr.Zero;
            globals["NullReferenceException"] = typeof(NullReferenceException);

            StringBuilder moduleCode = new StringBuilder();
            this.GenerateFunctions(moduleCode, methods, methodTable);

            PythonModule module = this.GetPythonContext().CreateModule(
                name, name, globals, ModuleOptions.PublishModule);
            this.ExecInModule(moduleCode.ToString(), module);
            return this.Store(module);
        }
        
        
        public override int 
        PyModule_AddObject(IntPtr modulePtr, string name, IntPtr itemPtr)
        {
            if (!this.ptrmap.ContainsKey(modulePtr))
            {
                return -1;
            }
            PythonModule module = (PythonModule)this.Retrieve(modulePtr);
            if (this.ptrmap.ContainsKey(itemPtr))
            {
                ScriptScope moduleScope = this.GetModuleScriptScope(module);
                moduleScope.SetVariable(name, this.Retrieve(itemPtr));
                this.DecRef(itemPtr);
            }
            else
            {
                IntPtr typePtr = CPyMarshal.ReadPtrField(itemPtr, typeof(PyTypeObject), "ob_type");
                if (typePtr == this.PyType_Type)
                {
                    this.GenerateClass(module, name, itemPtr);
                }
            }
            return 0;
        }
        
        
        private void
        GenerateCallablesFromMethodDefs(StringBuilder code, 
                        IntPtr methods, 
                        DispatchTable methodTable,
                        string tablePrefix, 
                        string noargsTemplate,
                        string objargTemplate, 
                        string varargsTemplate, 
                        string varargsKwargsTemplate)
        {
            IntPtr methodPtr = methods;
            if (methodPtr == IntPtr.Zero)
            {
                return;
            }
            while (CPyMarshal.ReadInt(methodPtr) != 0)
            {
                PyMethodDef thisMethod = (PyMethodDef)Marshal.PtrToStructure(
                    methodPtr, typeof(PyMethodDef));
                string name = thisMethod.ml_name;
                string template = null;
                Delegate dgt = null;
                
                bool unsupportedFlags = false;
                switch (thisMethod.ml_flags)
                {
                    case METH.NOARGS:
                        template = noargsTemplate;
                        dgt = Marshal.GetDelegateForFunctionPointer(
                            thisMethod.ml_meth, 
                            typeof(CPythonVarargsFunction_Delegate));
                        break;
                        
                    case METH.O:
                        template = objargTemplate;
                        dgt = Marshal.GetDelegateForFunctionPointer(
                            thisMethod.ml_meth, 
                            typeof(CPythonVarargsFunction_Delegate));
                        break;
                        
                    case METH.VARARGS:
                        template = varargsTemplate;
                        dgt = Marshal.GetDelegateForFunctionPointer(
                            thisMethod.ml_meth, 
                            typeof(CPythonVarargsFunction_Delegate));
                        break;
                        
                    case METH.VARARGS | METH.KEYWORDS:
                        template = varargsKwargsTemplate;
                        dgt = Marshal.GetDelegateForFunctionPointer(
                            thisMethod.ml_meth, 
                            typeof(CPythonVarargsKwargsFunction_Delegate));
                        break;
                        
                    default:
                        unsupportedFlags = true;
                        break;
                }
                
                if (!unsupportedFlags)
                {
                    code.Append(String.Format(template,
                        name, thisMethod.ml_doc, tablePrefix));
                    methodTable[tablePrefix + name] = dgt;
                }
                else
                {
                    Console.WriteLine("Detected unsupported method flags for {0}{1}; ignoring.",
                        tablePrefix, name);
                }
                
                methodPtr = CPyMarshal.Offset(methodPtr, Marshal.SizeOf(typeof(PyMethodDef)));
            }
        }
        
        private void 
        GenerateFunctions(StringBuilder code,  IntPtr methods, DispatchTable methodTable)
        {
            this.GenerateCallablesFromMethodDefs(
                code, methods, methodTable, "", 
                NOARGS_FUNCTION_CODE, 
                OBJARG_FUNCTION_CODE,
                VARARGS_FUNCTION_CODE, 
                VARARGS_KWARGS_FUNCTION_CODE);
        }

        private void
        GenerateMethods(StringBuilder code, IntPtr methods, DispatchTable methodTable, string tablePrefix)
        {
            this.GenerateCallablesFromMethodDefs(
                code, methods, methodTable, tablePrefix,
                NOARGS_METHOD_CODE,
                OBJARG_METHOD_CODE,
                VARARGS_METHOD_CODE,
                VARARGS_KWARGS_METHOD_CODE);
        }

        private void
        GenerateIterMethods(StringBuilder classCode, IntPtr typePtr, DispatchTable methodTable, string tablePrefix)
        {
            Py_TPFLAGS tp_flags = (Py_TPFLAGS)CPyMarshal.ReadIntField(typePtr, typeof(PyTypeObject), "tp_flags");
            if ((tp_flags & Py_TPFLAGS.HAVE_ITER) == 0)
            {
                return;
            }
            if (CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), "tp_iter") != IntPtr.Zero)
            {
                classCode.Append(String.Format(ITER_METHOD_CODE, tablePrefix));
                this.ConnectTypeField(typePtr, tablePrefix, "tp_iter", methodTable, typeof(CPythonSelfFunction_Delegate));
            }
            if (CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), "tp_iternext") != IntPtr.Zero)
            {
                classCode.Append(String.Format(ITERNEXT_METHOD_CODE, tablePrefix));
                this.ConnectTypeField(typePtr, tablePrefix, "tp_iternext", methodTable, typeof(CPythonSelfFunction_Delegate));
            }
        }

        private void
        ExtractNameModule(string tp_name, ref string __name__, ref string __module__)
        {
            string name = tp_name;
            string module = "";
            int lastDot = tp_name.LastIndexOf('.');
            if (lastDot != -1)
            {
                name = tp_name.Substring(lastDot + 1);
                module = tp_name.Substring(0, lastDot);
            }
            __name__ = name;
            __module__ = module;
        }
        
        private void
        GenerateClass(PythonModule module, string name, IntPtr typePtr)
        {
            StringBuilder classCode = new StringBuilder();
            string tablePrefix = name + ".";

            string tp_name = CPyMarshal.ReadCStringField(typePtr, typeof(PyTypeObject), "tp_name");
            string __name__ = null;
            string __module__ = null; ;
            this.ExtractNameModule(tp_name, ref __name__, ref __module__);
            
            string __doc__ = CPyMarshal.ReadCStringField(typePtr, typeof(PyTypeObject), "tp_doc").Replace("\\", "\\\\");
            classCode.Append(String.Format(CLASS_CODE, name, __module__, __doc__));

            ScriptScope moduleScope = this.GetModuleScriptScope(module);
            object _dispatcher = moduleScope.GetVariable<object>("_dispatcher");
            
            DispatchTable methodTable = (DispatchTable)Builtin.getattr(DefaultContext.Default, _dispatcher, "table");
            this.ConnectTypeField(typePtr, tablePrefix, "tp_new", methodTable, typeof(PyType_GenericNew_Delegate));
            this.ConnectTypeField(typePtr, tablePrefix, "tp_init", methodTable, typeof(CPython_initproc_Delegate));
            
            IntPtr methodsPtr = CPyMarshal.ReadPtrField(typePtr, typeof(PyTypeObject), "tp_methods");
            this.GenerateMethods(classCode, methodsPtr, methodTable, tablePrefix);
            this.GenerateIterMethods(classCode, typePtr, methodTable, tablePrefix);

            classCode.Append(String.Format(CLASS_FIXUP_CODE, name, __name__));
            this.ExecInModule(classCode.ToString(), module);

            object klass = moduleScope.GetVariable<object>(name);
            Builtin.setattr(DefaultContext.Default, klass, "_typePtr", typePtr);
            this.StoreUnmanagedData(typePtr, klass);
        }

        private void 
        ConnectTypeField(IntPtr typePtr, string tablePrefix, string fieldName, DispatchTable methodTable, Type dgtType)
        {
            Delegate dgt = CPyMarshal.ReadFunctionPtrField(
                typePtr, typeof(PyTypeObject), fieldName, dgtType);
            methodTable[tablePrefix + fieldName] = dgt;
        }
    }
}