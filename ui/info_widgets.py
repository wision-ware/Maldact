import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
from ui.ui_tools import dict_to_css
from event_bus import EventBus as eb
from functools import partial


class WarningText(qtw.QWidget):

    def __init__(self):

        super().__init__()



    def cleanup(self):
        pass