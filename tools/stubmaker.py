
import os
import sys

from tools.utils import popen, read_interesting_lines


def extract_funcname(c_func):
    # hacka hacka hacka
    return c_func.split('(')[0].split(' ')[-1].replace('*', '')


class StubMaker(object):

    def __init__(self, sourceLibrary=None, overrideDir=None):
        self.data = set()
        self.ordered_data = []
        self.functions = []
        self.mgd_functions = []

        if sourceLibrary is not None:
            self._read_symbol_table(sourceLibrary)

        if overrideDir is not None:
            self._read_overrides(overrideDir)


    def _read_symbol_table(self, source):
        if os.name == 'nt':
            self._read_symbol_table_pexports(source)
        elif os.name == 'posix':
            self._read_symbol_table_objdump(source)
        else:
            print 'Platform', os.name, 'is not supported'
            sys.exit(1)


    def _read_symbol_table_pexports(self, source):
        f = popen("pexports", source)
        try:
            for line in f:
                if line.strip() == 'EXPORTS':
                    break
            for line in f:
                line = line.strip()
                parts = line.split(' ')
                if len(parts) == 1:
                    self.functions.append(parts[0])
                else:
                    self.data.add(parts[0])
        finally:
            f.close()


    def _read_symbol_table_objdump(self, source):
        f = popen('objdump', '-T %s' % source)
        try:
            for line in f:
                if line.strip() == 'DYNAMIC SYMBOL TABLE:':
                    break
            for line in f:
                line = line.strip()
                if not line:
                    break
                flag = line[15]
                fields = line[17:].split()
                if len(fields) == 3:
                    section, size, name = fields
                if len(fields) == 4:
                    section, size, version, name = fields
                if section not in ('.bss', '.data', '.text'):
                    continue
                if flag == 'F':
                    self.functions.append(name)
                if flag == 'O':
                    self.data.add(name)
        finally:
            f.close()


    def _read_overrides(self, overrideDir):
        if not os.path.exists(overrideDir):
            return

        def tryread(filename):
            path = os.path.join(overrideDir, filename)
            if os.path.isfile(path):
                return read_interesting_lines(path)
            return []

        ignores = set(tryread("_dont_register_symbols"))
        self.functions = [f for f in self.functions if f not in ignores]
        
        self.mgd_functions = tryread("_mgd_function_prototypes")
        self.functions.extend(map(extract_funcname, self.mgd_functions))
        
        self.data |= set(tryread("_always_register_data_symbols"))
        self.data -= ignores
        self.ordered_data = tryread("_register_data_symbol_priority")
        self.data -= set(self.ordered_data)
        
                
    def generate_c(self):
        _init_proto = 'void init(void*(*address_getter)(char*), void(*data_setter)(char*, void*))'
        _boilerplate = 'void *jumptable[%%d];\n\n%s {\n' % _init_proto
        _init_data = '    data_setter("%s", &%s);\n'
        _init_function = '    jumptable[%s] = address_getter("%s");\n'

        result = [_boilerplate % len(self.functions)]
        result.extend([_init_data % (s, s) for s in self.ordered_data])
        result.extend([_init_data % (s, s) for s in self.data])
        for i, name in enumerate(self.functions):
            result.append(_init_function % (i, name))
        result.append('}\n')
        return ''.join(result)


    def generate_asm(self):
        _header = 'extern _jumptable\n\nsection .code\n\n'
        _declare = 'global _%s\n'
        _define = '_%s:\n    jmp [_jumptable+%d]\n'

        result = [_header]
        implementations = []
        for i, name in enumerate(self.functions):
            result.append(_declare % name)
            implementations.append(_define % (name, i * 4))
        result.append('\n')
        result.extend(implementations)
        return ''.join(result)

    def generate_header(self):
        return '\n'.join(self.mgd_functions) + '\n'