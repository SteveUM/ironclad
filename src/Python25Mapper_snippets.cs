namespace Ironclad
{
    public partial class Python25Mapper : PythonMapper
    {
        private const string MODULE_CODE = @"
from System import IntPtr

def _cleanup(*args):
    _ironclad_mapper.FreeTemps()
    for arg in args:
        if arg != IntPtr.Zero:
            _ironclad_mapper.DecRef(arg)

def _raiseExceptionIfRequired():
    error = _ironclad_mapper.LastException
    if error:
        _ironclad_mapper.LastException = None
        raise error
    
def _ironclad_dispatch(name, instancePtr, args):
    argPtr = _ironclad_mapper.Store(args)
    resultPtr = _ironclad_dispatch_table[name](instancePtr, argPtr)
    try:
        _raiseExceptionIfRequired()
        return _ironclad_mapper.Retrieve(resultPtr)
    finally:
        _cleanup(argPtr, resultPtr)
    
def _ironclad_dispatch_noargs(name, instancePtr):
    resultPtr = _ironclad_dispatch_table[name](instancePtr, IntPtr.Zero)
    try:
        _raiseExceptionIfRequired()
        return _ironclad_mapper.Retrieve(resultPtr)
    finally:
        _cleanup(resultPtr)

def _ironclad_dispatch_kwargs(name, instancePtr, args, kwargs):
    argPtr = _ironclad_mapper.Store(args)
    kwargPtr = IntPtr.Zero
    if len(kwargs):
        kwargPtr = _ironclad_mapper.Store(kwargs)
    resultPtr = _ironclad_dispatch_table[name](instancePtr, argPtr, kwargPtr)
    try:
        _raiseExceptionIfRequired()
        return _ironclad_mapper.Retrieve(resultPtr)
    finally:
        _cleanup(argPtr, kwargPtr, resultPtr)
    
";

        private const string NOARGS_FUNCTION_CODE = @"
def {0}():
    '''{1}'''
    return _ironclad_dispatch_noargs('{2}{0}', IntPtr.Zero)
";

        private const string OBJARG_FUNCTION_CODE = @"
def {0}(arg):
    '''{1}'''
    return _ironclad_dispatch('{2}{0}', IntPtr.Zero, arg)
";

        private const string VARARGS_FUNCTION_CODE = @"
def {0}(*args):
    '''{1}'''
    return _ironclad_dispatch('{2}{0}', IntPtr.Zero, args)
";

        private const string VARARGS_KWARGS_FUNCTION_CODE = @"
def {0}(*args, **kwargs):
    '''{1}'''
    return _ironclad_dispatch_kwargs('{2}{0}', IntPtr.Zero, args, kwargs)
";

        private const string CLASS_CODE = @"
class {0}(object):
    __module__ = '{1}'
    def __new__(cls, *args, **kwargs):
        instance = object.__new__(cls)
        argPtr = _ironclad_mapper.Store(args)
        kwargPtr = _ironclad_mapper.Store(kwargs)
        try:
            instancePtr = cls._tp_newDgt(cls._typePtr, argPtr, kwargPtr)
            _raiseExceptionIfRequired()
        finally:
            _cleanup(argPtr, kwargPtr)
        
        _ironclad_mapper.StoreUnmanagedData(instancePtr, instance)
        instance._instancePtr = instancePtr
        return instance
    
    def __init__(self, *args, **kwargs):
        object.__init__(self)
        argPtr = _ironclad_mapper.Store(args)
        kwargPtr = _ironclad_mapper.Store(kwargs)
        try:
            result = self.__class__._tp_initDgt(self._instancePtr, argPtr, kwargPtr)
            if result == -1:
                _ironclad_mapper.DecRef(self._instancePtr)
            _raiseExceptionIfRequired()
        finally:
            _cleanup(argPtr, kwargPtr)
";

        private const string NOARGS_METHOD_CODE = @"
    def {0}(self):
        '''{1}'''
        return _ironclad_dispatch_noargs('{2}{0}', self._instancePtr)
";

        private const string OBJARG_METHOD_CODE = @"
    def {0}(self, arg):
        '''{1}'''
        return _ironclad_dispatch('{2}{0}', self._instancePtr, arg)
";

        private const string VARARGS_METHOD_CODE = @"
    def {0}(self, *args):
        '''{1}'''
        return _ironclad_dispatch('{2}{0}', self._instancePtr, args)
";

        private const string VARARGS_KWARGS_METHOD_CODE = @"
    def {0}(self, *args, **kwargs):
        '''{1}'''
        return _ironclad_dispatch_kwargs('{2}{0}', self._instancePtr, args, kwargs)
";


        private const string CLASS_FIXUP_CODE = @"
{0}.__name__ = '{1}'
";
        
    }
}