import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
import signal_manager as sm
from ui.ui_switching import Main_modes


class Signal_manager(qtc.QObject):

    # ui signal dictionairy
    uis_dict = {}

    # menu signals

    uis_dict['menu'] = []

    t_switch = qtc.pyqtSignal(object)
    uis_dict['menu'].append([t_switch,Main_modes.switch_train])
    t_switch_trigger = lambda instance : custom_signal(t_switch,instance)
    t_switch.connect(Main_modes.switch_train)

    c_switch = qtc.pyqtSignal(object)
    uis_dict['menu'].append(c_switch)
    c_switch_trigger = lambda instance : custom_signal(c_switch,instance)

    # training signals

    uis_dict['train'] = []

    # sorting signals

    uis_dict['sort'] = []

def custom_signal(signal,parameter):
    signal.emit(parameter)

def connect_signals(ui):
    pass

def disconnect_signals(ui):
    pass

signal_manager = Signal_manager