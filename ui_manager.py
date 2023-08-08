import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from ui.ui_builder import Transitions
from event_bus import EventBus as eb


class UIManager:

    @classmethod
    def initialize(cls, main_window):
        from ui.main_window import Menu
        cls.main_window = main_window
        cls.active_ui = cls.main_window.centralWidget()
        cls.transitions = Transitions(main_window, cls.active_ui)
        cls.switch_ui(Menu)
        eb.subscribe("switch_modes", cls.switch_ui)

    # switches from a chosen displayed widget to a different one
    @classmethod
    def switch_widget(cls, replaced, new):
        cls.disconnect_signals(replaced)
        cls.transitions.switch_widget(replaced, new)
        cls.connect_signals(new)

    # switches from a chosen ui page to a different one
    @classmethod
    def switch_ui(cls, ui_class=None):

        if isinstance(ui_class,dict):
            ui_class = ui_class['ui_cls']

        elif isinstance(ui_class, type(None)):
            return

        if cls.active_ui:
            cls.disconnect_signals(cls.active_ui)

        cls.transitions.switch_ui(ui_class())
        cls.connect_signals(ui_class())

    @classmethod
    def disconnect_signals(cls, ui):
        from signal_manager import SignalManager as sm
        # Disconnect any signals that were connected to the previous UI
        sm.disconnect_signals(ui.key)

    @classmethod
    def connect_signals(cls, ui):
        from signal_manager import SignalManager as sm
        # Disconnect any signals that were connected to the previous UI
        sm.connect_signals(ui.key)