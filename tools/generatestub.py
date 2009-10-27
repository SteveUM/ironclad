
import os, sys

from tools.stubmaker import StubMaker
from tools.utils import write

src_dll, src, dst = sys.argv[1:]
dst_include = os.path.join(dst, 'Include')

sm = StubMaker(src_dll, src)
write(dst, "stubinit.generated.c", sm.generate_c(), badge=True)
write(dst, "jumps.generated.asm", sm.generate_asm(), badge=True)
write(dst_include, "_mgd_function_prototypes.generated.h", sm.generate_header(), badge=True)
