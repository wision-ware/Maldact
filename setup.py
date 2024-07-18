from setuptools import setup, find_packages

setup(
    name='maldact',
    version='0.1.0',
    packages=find_packages('src'),
    package_dir={'': 'src'},
    include_package_data=True,
    install_requires=[
    ],
    entry_points={
        'console_scripts': [
            'maldact=maldact.main:main'
        ],
    },
    author='Vavrinec Kavan',
    author_email='vavrinec.kavan@gmail.com',
    description='Machine learning data classification tool',
    long_description=open('README.md').read(),
    long_description_content_type='text/markdown',
    url='https://github.com/wision-ware/Maldact',
    classifiers=[
        'Programming Language :: Python :: 3.11',
        'License :: OSI Approved :: MIT License',
        'Operating System :: OS Independent',
    ],
    python_requires='>=3.10',
)