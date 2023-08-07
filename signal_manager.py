import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from ui_manager import UIManager as um


class SignalManager(qtc.QObject):

    @classmethod
    def custom_signal(cls, signal, parameter):
        signal.emit(parameter)

    @classmethod
    def connect_signals(cls, ui):

        for signal, slot in cls.uis_dict[ui]:
            signal.connect(slot)

    @classmethod
    def disconnect_signals(cls, ui):

        for signal, slot in cls.uis_dict[ui]:
            signal.connect(slot)

    # ui signal dictionairy
    uis_dict = {
        'menu': [],
        'train': [],
        'sort': []
    }

    # menu signals

    t_switch = qtc.pyqtSignal(object)
    uis_dict['menu'].append((t_switch, um.switch_ui))
    t_switch_trigger = lambda instance : custom_signal(t_switch,instance)

    c_switch = qtc.pyqtSignal(object)
    uis_dict['menu'].append((c_switch, um.switch_ui))
    c_switch_trigger = lambda instance : custom_signal(c_switch,instance)

    # training signals

    #----

    # sorting signals

    #----