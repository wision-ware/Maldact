import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
from ui.ui_tools import dict_to_css
from event_bus import EventBus as eb
from functools import partial


class WarningText(qtw.QWidget):

    def __init__(self, initial_message):

        super().__init__()
        self.main_layout = qtw.QVBoxLayout(self)

        # main layout assembling

        message = qtw.QLabel(initial_message)
        message.setStyleSheet(f"color: red; font: Fira Code")
        self.main_layout.addWidget(message)

    def cleanup(self):
        pass