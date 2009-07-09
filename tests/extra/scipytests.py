import os.path
import sys
from tests.utils.schnoz import Schnoz


if sys.platform == 'cli':
    # we expect this to be run from project root
    sys.path.insert(0, "build")
    import ironclad
    ironclad.patch_native_filenos()


import scipy
scipy_path = r"C:\Python25\Lib\site-packages\scipy"
dirs = [
    'cluster', 'fftpack', 'integrate', 'io', 'maxentropy', 
    'misc', 'ndimage', 'odr', 'optimize', 'special', 'stats'
]
# 'interpolate', 'signal', # cannot import factorial from scipy
# 'sparse', 'spatial', # slow and boring, but haven't seen any failures



if __name__ == "__main__":
    scipytester = Schnoz(name="scipy", lib_path=scipy_path, data_dir=os.path.dirname(__file__))
    scipytester.main(dirs, 2)

