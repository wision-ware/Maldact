import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys


class SignalManager(qtc.QObject):

    @classmethod
    def custom_signal(cls, signal, parameter):
        signal.emit(parameter)

    @classmethod
    def connect_signals(cls, ui):
        if len(cls.uis_dict[ui]) != 0:
            for signal, slot in cls.uis_dict[ui]:
                signal.connect(slot)

    @classmethod
    def disconnect_signals(cls, ui):
        if len(cls.uis_dict[ui]) != 0:
            for signal, slot in cls.uis_dict[ui]:
                signal.connect(slot)

    # ui signal dictionairy
    uis_dict = {
        'menu': [],
        'train': [],
        'sort': []
    }

    "custom signal definition example---------------------------------"
    # signal = qtc.pyqtSignal(object)
    # uis_dict['category'].append((signal, um.switch_ui))
    # signal_trigger = lambda instance : custom_signal(signal,instance)
    "-----------------------------------------------------------------"

    # menu signals

    #----

    # training signals

    #----

    # sorting signals

    #----