import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from signal_manager import SignalManager as sm
from widgets import UiMModeButtons, UiFileSelector

class Menu(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)
        self.key = 'menu'

        # sublayout assembling
        ## sublayout for mode buttons

        # ----

        # main layout assembling

        main_layout.addWidget(UiMModeButtons)


class Training(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)
        self.key = 'train'

        # sublayout assembling
        ## sublayout for mode buttons

        # ----

        # main layout assembling

        main_layout.addWidget(UiFileSelector)


class Sorting(qtw.QWidget):

    def __init__(self):

        super().__init__()

        main_layout = qtw.QVBoxLayout(self)
        self.key = 'sort'

        # sublayout assembling
        ## sublayout for mode buttons

        # ----

        # main layout assembling