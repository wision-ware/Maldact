
import PyQt5.QtWidgets as qtw
import PyQt5.QtGui as qtg
import PyQt5.QtCore as qtc
import os, sys
from maldact.ui.input_widgets import *
from maldact.ui.info_widgets import *
from maldact.event_bus import EventBus as eb
from maldact.backend.ml_managers import TrainingManager, SortingManager


class LoadingWindow(qtw.QDialog):

    def __init__(self, info: str, close_event: str, cancel_receiver: str):
        
        super().__init__()

        self.main_layout = qtw.QVBoxLayout(self)
        self.sub_layout_1 = qtw.QHBoxLayout(self)
        self.sub_layout_2 = qtw.QHBoxLayout(self)
        self.loading_gif_path = "../resources/loading.gif"
        self.close_event = close_event
        self.cancel_receiver = cancel_receiver

        self.setWindowTitle("Loading...")

        # sub layout assembly
        # 1

        self.loading_gif_label = qtw.QLabel(self)
        self.loading_gif = qtg.QMovie(self.loading_gif_path)
        self.sub_layout_1.addWidget(self.loading_gif_label)
        self.loading_gif_label.setMovie(self.loading_gif)
        self.loading_gif.start()

        self.content_text = qtw.QLabel(info)
        self.sub_layout_1.addWidget(self.content_text)

        # 2

        self.sub_layout_2.addStretch(1)

        self.cancel_button = qtw.QPushButton("Cancel")
        self.cancel_button.clicked.connect(self.terminate)
        self.sub_layout_2.addWidget(self.cancel_button)

        # main layout assembly

        self.main_layout.addLayout(self.sub_layout_1)
        self.main_layout.addStretch(1)

        self.setLayout(self.main_layout)

        # event based termination

        eb.subscribe(self.close_event, self.terminate)

    def terminate(self):

        eb.unsubscribe(self.close_event, self.terminate)
        eb.emit(self.cancel_receiver)
        self.accept()
