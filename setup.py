
from setuptools import setup, find_packages

# Load the README file for the long description
with open('README.md', 'r', encoding='utf-8') as f:
    long_description = f.read()

setup(
    name='maldact',
    version='0.1.0',
    packages=find_packages('src'),
    package_dir={'': 'src'},
    include_package_data=True,
    install_requires=[
        # List your required dependencies here
        'numpy',
        'matplotlib',
        'pyqt5',
        'pytest',
        'cupy'
    ],
    entry_points={
        'console_scripts': [
            'maldact=maldact.main:main'
        ],
    },
    author='Vavřinec Kavan',
    author_email='vavrinec.kavan@gmail.com',
    description='Machine learning data classification tool',
    long_description=long_description,
    long_description_content_type='text/markdown',
    url='https://github.com/wision-ware/Maldact',
    project_urls={
        'Documentation': 'https://github.com/wision-ware/Maldact',
        'Source': 'https://github.com/wision-ware/Maldact',
        'Tracker': 'https://github.com/wision-ware/Maldact/issues',
        'Institute': 'https://www.ufa.cas.cz/en/institute-structure/department-of-space-physics/'
    },
    classifiers=[
        'Development Status :: 3 - Alpha',
        'Intended Audience :: Developers',
        'Intended Audience :: Science/Research',
        'License :: OSI Approved :: MIT License',
        'Programming Language :: Python :: 3.11',
        'Programming Language :: Python :: 3 :: Only',
        'Operating System :: OS Independent',
        'Topic :: Software Development :: Libraries',
        'Topic :: Scientific/Engineering :: Artificial Intelligence',
    ],
    keywords='machine learning, data classification, ANN, GUI, CLI',
    python_requires='>=3.10',
    extras_require={
        'dev': ['check-manifest'],
        'test': ['coverage'],
    },
    license='MIT',
    license_files=('LICENSE',),
    test_suite='tests',
    maintainer='Department of Space Physics, Institute of Atmospheric Physics, CAS',
    maintainer_email='ufa@ufa.cas.cz',
)