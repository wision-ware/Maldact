import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
import signal_manager as sm
from ui.ui_switching import Main_modes

class UIManager:

    def __init__(self, main_window):

        self.main_window = main_window
        self.active_ui = None

    def switch_ui(self, new_ui_class):

        if self.active_ui:
            self.disconnect_signals(self.active_ui)
            self.main_window.centralWidget().layout().removeWidget(self.active_ui)
            self.active_ui.deleteLater()

        self.active_ui = new_ui_class()
        self.main_window.centralWidget().layout().addWidget(self.active_ui)

    def disconnect_signals(self, ui):
        # Disconnect any signals that were connected to the previous UI
        pass