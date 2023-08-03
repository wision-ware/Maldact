import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from ui.ui_builder import Transitions


class SignalManager(qtc.QObject):

    @classmethod
    def custom_signal(cls, signal, parameter):
        signal.emit(parameter)

    @classmethod
    def connect_signals(cls, ui):

        for sigslot in uis_dict[ui]:
            signal, slot = sigslot
            signal.connect(slot)

    @classmethod
    def disconnect_signals(cls, ui):

        for sigslot in uis_dict[ui]:
            signal, slot = sigslot
            signal.disconnect(slot)

    # ui signal dictionairy
    uis_dict = {
        'menu': [],
        'train': [],
        'sort': []
    }

    # menu signals

    t_switch = qtc.pyqtSignal(object)
    uis_dict['menu'].append((t_switch,MainModes.switch_train))
    t_switch_trigger = lambda instance : custom_signal(t_switch,instance)

    c_switch = qtc.pyqtSignal(object)
    uis_dict['menu'].append((c_switch, MainModes.switch_sort))
    c_switch_trigger = lambda instance : custom_signal(c_switch,instance)

    # training signals

    #----

    # sorting signals

    #----
