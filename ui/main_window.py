import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from ui.widgets import *


class Menu(qtw.QWidget):

    def __init__(self):

        super().__init__()

        self.main_layout = qtw.QVBoxLayout(self)
        self.key = 'menu'
        self.subscribe_list = ["switch_modes"]

        # sublayout assembling

        # ----

        # main layout assembling

        self.main_layout.addWidget(CustomHeader("Select mode"))

        self.main_layout.addWidget(LargeButtons(
            button1 = (
                self.subscribe_list[0],
                {"ui_cls":Training}
            ),
            button2 = (
                self.subscribe_list[0],
                {"ui_cls":Sorting}
            ),
            labels = (
                "Train a classification model",
                "Classify a dataset"
            )
        ))


class Training(qtw.QWidget):

    def __init__(self):

        super().__init__()

        self.main_layout = qtw.QVBoxLayout(self)
        self.sub_layout1 = qtw.QHBoxLayout(self)
        self.key = 'train'

        # sublayout assembling

        self.sub_layout1.addWidget(TitledLineEdit(
            labels = ("Select architecture:","example: [:,50,50,50,5]"),
            layout = 'v',
            line_edited = "arch_line_edited"
        ))
        self.sub_layout1.addWidget(TitledDropdown(
            labels = ("Select gradient descent type:", "Mini Batch"),
            layout = 'v',
            options = (
                "Batch",
                "Mini Batch",
                "Stochastic"
            )
        ))

        # main layout assembling
        # header
        self.main_layout.addWidget(CustomHeader("Model training mode"))

        # content
        self.main_layout.addWidget(TitledLineEdit(
            line_edited = "model_name_entered",
            labels = ("Select model name:", "model1"),
        ))

        self.main_layout.addWidget(FileSelector(
            file_selected = "tr_samples_selected",
            labels = "Select training data directory:",
            directory = True
        ))

        self.main_layout.addWidget(FileSelector(
            file_selected = "parameter_dir_selected",
            labels = "Select a directory for your model:",
            directory = True
        ))

        self.main_layout.addLayout(self.sub_layout1)

        # footer
        self.main_layout.addStretch(1)
        self.main_layout.addWidget(CustomFooter((
            ("back to menu", "switch_modes", {"ui_cls":Menu}),
            "start training"
        )))


class Sorting(qtw.QWidget):

    def __init__(self):

        super().__init__()

        self.main_layout = qtw.QVBoxLayout(self)
        self.key = 'sort'

        # sublayout assembling

        # ----

        # main layout assembling

        self.main_layout.addWidget(CustomHeader("Data classification mode"))

        self.main_layout.addWidget(FileSelector(
            file_selected = "sample_dir_selected",
            labels = "Select directory with samples for classification:",
            directory = True
        ))

        self.main_layout.addWidget(FileSelector(
            file_selected = "model_selected",
            labels ="Select your trained model:"
        ))

        self.main_layout.addStretch(1)

        self.main_layout.addWidget(CustomFooter((
            ("back to menu", "switch_modes", {"ui_cls":Menu}),
            "start classification"
        )))