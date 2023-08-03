import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from signal_manager import SignalManager as sm
from ui.ui_builder import Transitions


class UIManager:

    @classmethod
    def initialize(cls, main_window):
        cls.main_window = main_window
        cls.active_ui = None
        Transitions.initialize(main_window, cls.active_ui)

    # switches from a chosen displayed widget to a different one
    @classmethod
    def switch_widget(cls, replaced, new):

        if cls.active_ui:
            cls.disconnect_signals(cls.active_ui)
        Transitions.replace_ui(replaced, new)

    # switches from a chosen ui page to a different one
    @classmethod
    def switch_ui(cls, ui_class):
        replaced = main_window.remove(Centr)

        if cls.active_ui:
            cls.disconnect_signals(cls.active_ui)
        Transitions.replace_ui(main_window.layout())

    @classmethod
    def disconnect_signals(cls, ui):
        # Disconnect any signals that were connected to the previous UI
        sm.disconnect_signals(ui.key)