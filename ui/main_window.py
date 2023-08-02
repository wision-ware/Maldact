import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
import signal_manager as sm
from ui.ui_switching import Main_modes
from widgets import Ui_MModeButtons, Ui_FileSelector

class Menu(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)

        # sublayout assembling
        ## sublayout for mode buttons

        # ----

        # main layout assembling

        main_layout.addWidget(Ui_MModeButtons)

class Training(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)

        # sublayout assembling
        ## sublayout for mode buttons

        # ----

        # main layout assembling

        main_layout.addWidget(Ui_FileSelector)

class Sorting(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)

        # sublayout assembling
        ## sublayout for mode buttons

        # ----

        # main layout assembling