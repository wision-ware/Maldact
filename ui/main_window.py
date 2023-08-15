import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from ui.widgets import *
from event_bus import EventBus as eb


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
        self.sub_layout2 = qtw.QHBoxLayout(self)
        self.key = 'train'

        # sublayout assembling
        # sublayout 1

        self.sub_layout1.addWidget(TitledLineEdit(
            labels = ("Select architecture:","example: [:,50,50,50,5]"),
            layout = 'v',
            line_edited = "arch_line_edited"
        ))
        self.sub_layout1.addWidget(TitledDropdown(
            labels = "Select gradient descent type:",
            layout = 'v',
            options = (
                "Batch",
                "Mini Batch",
                "Stochastic"
            )
        ))

        # sublayout 2

        options = (
            "Time limit",
            "Time limit with cost treshold",
            "Cost treshold",
            "Fixed number of iterations"
        )
        self.sub_layout2.addWidget(TitledDropdown(
            option_changed = "train_switch_ledit",
            labels = "Select termination condition:",
            layout = 'v',
            options = options
        ))

        self.switched_linedit = TitledLineEdit()
        self.switched_linedit.setEnabled(False)
        self.sub_layout2.addWidget(self.switched_linedit, alignment=qtc.Qt.AlignBottom)

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

        self.main_layout.addLayout(self.sub_layout2)

        # footer
        self.main_layout.addStretch(1)
        self.main_layout.addWidget(CustomFooter((
            ("back to menu", "switch_modes", {"ui_cls":Menu}),
            "start training"
        )))

        self.switch_widget(options[0], old_widget=self.switched_linedit, parent=self.sub_layout2, stored = 'termination_options')
        # subscriptions to event bus
        eb.subscribe("train_switch_ledit",
                     lambda key, parent=self.sub_layout2 : self.switch_widget(
                         key,
                         parent = self.sub_layout2,
                         stored = "termination_options"
                     ))

    def switch_widget(self, key, old_widget=None, parent=None, stored=None):

        # define widget switching mechanism

        self.term_cond_assoc = {}

        self.term_cond_assoc["Time limit"] = TitledLineEdit(
            labels=("Set time limit for trining", "time (s)"),
            layout='v'
        )

        self.term_cond_assoc["Time limit with cost treshold"] = DefaultWidget(layout='h')
        self.term_cond_assoc["Time limit with cost treshold"].main_layout.addWidget(
            TitledLineEdit(
                labels=("Set time limit for training", "time (s)"),
                layout='v'
            )
        )
        self.term_cond_assoc["Time limit with cost treshold"].main_layout.addWidget(
            TitledLineEdit(
                labels=("Enter the order of cost treshold:", "default: -10"),
                layout='v'
            )
        )

        self.term_cond_assoc["Cost treshold"] = TitledLineEdit(
            labels=("Enter the order of cost treshold:", "default: -10"),
            layout='v'
        )

        self.term_cond_assoc["Fixed number of iterations"] = TitledLineEdit(
            labels=("Enter the number of iterations:", "default: -10"),
            layout='v'
        )

        # request a switch to ui manager
        eb.emit("switch_widgets", old_widget, self.term_cond_assoc[key], parent, stored=stored)

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