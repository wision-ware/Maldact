import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
from ui.input_widgets import *
from ui.info_widgets import *
from event_bus import EventBus as eb
from backend.ml_managers import TrainingManager, SortingManager


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
            button1=(
                self.subscribe_list[0],
                {"ui_cls": Training}
            ),
            button2=(
                self.subscribe_list[0],
                {"ui_cls": Sorting}
            ),
            labels=(
                "Train a classification model",
                "Classify a dataset"
            )
        ))

    @staticmethod
    def cleanup():
        pass


class Training(qtw.QWidget):

    def __init__(self):
        super().__init__()

        self.training_manager = TrainingManager()

        # user input storage
        self.input_dict = {}

        # layout specification
        self.main_layout = qtw.QVBoxLayout(self)
        self.sub_layout1 = qtw.QHBoxLayout(self)
        self.sub_layout2 = qtw.QHBoxLayout(self)
        self.key = 'train'
        self.exclusion = ["threshold", "time_limit", "fixed_iter"]

        # sub layout assembling
        # sub layout 1

        self.sub_layout1.addWidget(TitledLineEdit(
            labels=("Select architecture:", "example: [:,50,50,50,5]"),
            layout='v',
            line_edited=("store_tr", {"attr": "N"})
        ))
        self.sub_layout1.addWidget(TitledDropdown(
            labels="Select gradient descent type:",
            layout='v',
            option_changed=('store_tr', {"attr": "GD"}),
            options=(
                "Batch",
                "Mini Batch",
                "Stochastic"
            )
        ))

        # sub layout 2

        options = (
            "Time limit",
            "Time limit with cost threshold",
            "Cost threshold",
            "Fixed number of iterations"
        )
        self.sub_layout2.addWidget(TitledDropdown(
            option_changed=("switch_channel1", None),
            labels="Select termination condition:",
            layout='v',
            options=options
        ))

        self.switched_linedit = TitledLineEdit()
        self.switched_linedit.setEnabled(False)
        self.sub_layout2.addWidget(self.switched_linedit)

        # main layout assembling
        # header
        self.main_layout.addWidget(CustomHeader("Model training mode"))

        # content
        self.main_layout.addWidget(TitledLineEdit(
            line_edited=("store_tr", {"attr": "model_name"}),
            labels=("Select model name:", "model1"),
        ))

        self.main_layout.addWidget(FileSelector(
            file_selected=("store_tr", {"attr": "data_dir"}),
            labels="Select training data directory:",
            directory=True
        ))

        self.main_layout.addWidget(FileSelector(
            file_selected=("store_tr", {"attr": "model_dir"}),
            labels="Select a directory for your model:",
            directory=True
        ))

        self.main_layout.addLayout(self.sub_layout1)

        self.main_layout.addLayout(self.sub_layout2)

        # footer
        self.main_layout.addStretch(1)
        self.main_layout.addWidget(CustomFooter((
            ("back to menu", "switch_modes", {"ui_cls": Menu}),
            ("start training", "start_training")
        )))

        self.switch_widget(
            options[0],
            old_widget=self.switched_linedit,
            parent=self.sub_layout2,
            stored='termination_options'
        )

        # subscriptions to event bus
        self.switch_func1 = lambda key, old_widget, parent=self.sub_layout2, stored="termination_options": self.switch_widget(
            key,
            old_widget,
            parent=parent,
            stored=stored
        )

        eb.subscribe(
            "switch_channel1",
            self.switch_func1
        )
        eb.subscribe(
            "store_tr",
            self.store_user_input
        )
        eb.subscribe(
            "start_training",
            self.start_training
        )

    def switch_widget(self, key, old_widget=None, parent=None, stored=None):
        # define widget switching mechanism

        self.term_cond_assoc = {}

        self.term_cond_assoc["Time limit"] = TitledLineEdit(
            labels=("Set time limit for training", "time (s)"),
            line_edited=("store_tr", {"attr": "time_limit"}),
            layout='v'
        )

        self.term_cond_assoc["Time limit with cost threshold"] = DefaultWidget(layout='h')
        self.term_cond_assoc["Time limit with cost threshold"].main_layout.addWidget(
            TitledLineEdit(
                labels=("Set time limit for training", "time (s)"),
                line_edited=("store_tr", {"attr": "time_limit"}),
                layout='v'
            ),
            alignment=qtc.Qt.AlignTop
        )
        self.term_cond_assoc["Time limit with cost threshold"].main_layout.addWidget(
            TitledLineEdit(
                labels=("Enter cost threshold:", "default: 10e-10"),
                line_edited=("store_tr", {"attr": "threshold"}),
                layout='v'
            ),
            alignment=qtc.Qt.AlignTop
        )

        self.term_cond_assoc["Cost threshold"] = TitledLineEdit(
            labels=("Enter cost threshold:", "default: 10e-10"),
            line_edited=("store_tr", {"attr": "threshold"}),
            layout='v'
        )

        self.term_cond_assoc["Fixed number of iterations"] = TitledLineEdit(
            labels=("Enter the number of iterations:", "default: 1000"),
            line_edited=("store_tr", {"attr": "fixed_iter"}),
            layout='v'
        )

        # request a switch to ui manager
        eb.emit("switch_widgets", old_widget, self.term_cond_assoc[key], parent, stored=stored)

    @staticmethod
    def cleanup():
        eb.unsubscribe(
            "switch_channel1",
            True
        )
        eb.unsubscribe(
            "store_tr",
            True
        )
        eb.unsubscribe(
            "start_training",
            True
        )

    def store_user_input(self, value, meta):
        self.input_dict[meta["attr"]] = value

    def start_training(self):
        self.training_manager.update_params(self.input_dict)
        self.training_manager.start_training()


class Sorting(qtw.QWidget):

    def __init__(self):
        super().__init__()

        self.sorting_manager = SortingManager()

        self.main_layout = qtw.QVBoxLayout(self)
        self.key = 'sort'
        self.input_dict = {}

        # sublayout assembling

        # ----

        # main layout assembling

        self.main_layout.addWidget(CustomHeader("Data classification mode"))

        self.main_layout.addWidget(FileSelector(
            file_selected=("store_st", {"attr": "sort_dir"}),
            labels="Select directory for the sorting folders:",
            directory=True
        ))

        self.main_layout.addWidget(FileSelector(
            file_selected=("store_st", {"attr": "model_file"}),
            labels="Select your trained model:"
        ))

        self.main_layout.addStretch(1)

        self.main_layout.addWidget(CustomFooter((
            ("back to menu", "switch_modes", {"ui_cls": Menu}),
            ("start sorting", "start_sorting")
        )))

        eb.subscribe(
            "store_tr",
            self.store_user_input
        )
        eb.subscribe(
            "start_sorting",
            self.start_sorting
        )

    def store_user_input(self, value, meta):
        self.input_dict[meta["attr"]] = value

    def start_sorting(self):
        self.sorting_manager.update_params(self.input_dict)
        self.sorting_manager.start_sorting()

    @staticmethod
    def cleanup():
        eb.unsubscribe(
            "store_tr",
            True
        )
        eb.unsubscribe(
            "start_sorting",
            True
        )
