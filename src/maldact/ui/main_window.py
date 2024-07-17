import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os, sys
import traceback

from ui.input_widgets import *
from ui.info_widgets import *
from ui.popups import *
from event_bus import EventBus as eb
from backend.ml_managers import TrainingManager, SortingManager
import numpy as np
import pickle


class Menu(qtw.QWidget):

    def __init__(self) -> None:
        super().__init__()

        self.main_layout = qtw.QVBoxLayout(self)
        self.key = 'menu'
        self.subscribe_list = ["switch_modes"]

        # sub layout assembling

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
    def cleanup() -> None:
        pass


class Training(qtw.QWidget):

    def __init__(self) -> None:
        super().__init__()

        self.training_popup = None
        self.term_cond_assoc = {}
        self.warning_state = False
        self.input_warning_text = None

        # user input storage
        self.input_dict = {}

        # layout specification
        self.main_layout = qtw.QVBoxLayout(self)
        self.sub_layout1 = qtw.QHBoxLayout(self)
        self.sub_layout2 = qtw.QHBoxLayout(self)
        self.key = 'train'
        self.exclusion = ["threshold", "time_limit", "fixed_iter"]

        # subscriptions to event bus
        self.switch_func1 = lambda key, old_widget, parent=self.sub_layout2, stored="termination_options": \
            self.switch_termination_input(key, old_widget, parent=parent, stored=stored)

        self.subs: list = [
            ("switch_channel1", self.switch_func1),
            ("store_tr", self.store_user_input),
            ("start_training", self.start_training)
        ]

        for sub in self.subs:
            eb.subscribe(*sub)

        # sub layout assembling
        # sub layout 1

        self.sub_layout1.addWidget(t := TitledDropdown(
            labels="Select gradient descent type:",
            layout='v',
            option_changed=('store_tr', {"attr": "GD"}),
            options=(
                "Batch",
                "Mini Batch",
                "Stochastic"
            )
        ))
        t.trigger()

        self.sub_layout1.addWidget(TitledLineEdit(
            labels=("Set architecture:", "example: [:,50,50,50,5]"),
            layout='v',
            line_edited=("store_tr", {"attr": "N"}),
            border="grey_round"
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
        self.default_model_name = "model<number>"

        self.main_layout.addWidget(t := TitledLineEdit(
            line_edited=("store_tr", {"attr": "model_name"}),
            labels=("Select model name:", self.default_model_name),
        ))
        t.trigger()

        self.file_selection_container = DefaultWidget(
            border="grey_round",
            padding=0,
            layout="v"
        )

        self.file_selection_container.main_layout.addWidget(
            FileSelector(file_selected=("store_tr", {"attr": "data_file"}), labels="Select training data file:",
                         file_type="npy"))

        self.file_selection_container.main_layout.addWidget(
            FileSelector(file_selected=("store_tr", {"attr": "model_dir"}), labels="Select a directory for your model:",
                         file_type="[DIR]"))
        self.main_layout.addWidget(self.file_selection_container)

        self.main_layout.addLayout(self.sub_layout1)

        self.main_layout.addLayout(self.sub_layout2)

        # footer
        self.main_layout.addStretch(1)
        self.main_layout.addWidget(CustomFooter((
            ("back to menu", "switch_modes", {"ui_cls": Menu}),
            ("start training", "start_training")
        )))

        self.switch_termination_input(options[0], old_widget=self.switched_linedit, parent=self.sub_layout2,
                                      stored='termination_options')

    def switch_termination_input(self, key: str, old_widget=None, parent=None, stored=None) -> None:

        # replacement widgets
        self.term_cond_assoc["Time limit"] = TitledLineEdit(
            labels=("Set time limit for training", "time (s)"),
            line_edited=("store_tr", {"attr": "time_limit"}),
            layout="v",
            border="grey_round"
        )

        self.term_cond_assoc["Time limit with cost threshold"] = DefaultWidget(layout='h', padding=0)
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
                labels=("Enter cost threshold:", "int or float < 1"),
                line_edited=("store_tr", {"attr": "threshold"}),
                layout='v'
            ),
            alignment=qtc.Qt.AlignTop
        )

        self.term_cond_assoc["Cost threshold"] = TitledLineEdit(
            labels=("Enter cost threshold:", "int or float < 1"),
            line_edited=("store_tr", {"attr": "threshold"}),
            layout="v",
            border="grey_round"
        )

        self.term_cond_assoc["Fixed number of iterations"] = TitledLineEdit(
            labels=("Enter the number of iterations:", "integer"),
            line_edited=("store_tr", {"attr": "fixed_iter"}),
            layout="v",
            border="grey_round"
        )

        # request a switch to ui manager
        eb.emit("switch_widgets", old_widget, self.term_cond_assoc[key], parent, stored=stored)

    def cleanup(self) -> None:
        for sub in self.subs:
            eb.unsubscribe(sub[0], True)

    def store_user_input(self, value, meta) -> None:
        self.input_dict[meta["attr"]] = value

    def start_training(self) -> None:

        if all(key in self.input_dict for key in ("model_dir", "data_file")):

            if self.warning_state is True:
                self.main_layout.removeWidget(self.input_warning_text)
                self.input_warning_text.deleteLater()
                self.warning_state = False

            try:
                _ = np.load(self.input_dict["data_file"], allow_pickle=True)

            except (AssertionError, AttributeError, FileNotFoundError, OSError, IOError, pickle.UnpicklingError):
                self.input_warning_text = WarningText("Files failed to load!")
                last_index = self.main_layout.count()
                self.main_layout.insertWidget(last_index - 1, self.input_warning_text, alignment=qtc.Qt.AlignRight)
                self.warning_state = True
                return None

            new_manager = TrainingManager(default_model_name=self.default_model_name)
            new_manager.update_params(self.input_dict)

            subscription = (f"training_crashed_{new_manager.id}", self.on_training_crash)
            self.subs.append(subscription)
            eb.subscribe(*subscription)

            new_manager.start_training()

            self.training_popup = LoadingWindow(f'''Training of the "{new_manager.model_name}" model in progress''',
                                                f"training_done_{new_manager.id}",
                                                f"training_canceled_{new_manager.id}")
            self.training_popup.setModal(False)
            self.training_popup.exec()

        elif self.warning_state is False:

            self.input_warning_text = WarningText("Mandatory input missing!")
            last_index = self.main_layout.count()
            self.main_layout.insertWidget(last_index - 1, self.input_warning_text, alignment=qtc.Qt.AlignRight)
            self.warning_state = True

        else:
            return None

    def on_training_crash(self, exception_info, manager_id):
        eb.emit(f"training_done_{manager_id}")
        eb.unsubscribe(f"training_crashed_{manager_id}", self.on_training_crash)

        exc_type = exception_info.get("type", "Exception")
        exc_msg = exception_info.get("message", "Unknown error")
        exc_traceback = exception_info.get("traceback", "")

        if exc_type in __builtins__:
            exc_class = __builtins__[exc_type]
        else:
            exc_class = Exception
        print(exc_traceback, file=sys.stderr)
        raise exc_class(exc_msg) from None


class Sorting(qtw.QWidget):

    def __init__(self) -> None:
        super().__init__()

        self.sorting_manager = SortingManager()

        self.main_layout = qtw.QVBoxLayout(self)
        self.key = 'sort'
        self.input_dict = {}
        self.warning_state = False
        self.input_warning_text = None
        self.sorting_popup = None

        # sub layout assembling

        # ----

        # main layout assembling

        self.main_layout.addWidget(CustomHeader("Data classification mode"))

        self.file_selection_container = DefaultWidget(
            border="grey_round",
            padding=0,
            layout="v"
        )

        self.file_selection_container.main_layout.addWidget(
            FileSelector(
                file_selected=("store_st", {"attr": "sort_dir"}),
                labels="Select directory for the sorting folders:",
                file_type="[DIR]"
            )
        )

        self.file_selection_container.main_layout.addWidget(
            FileSelector(
                file_selected=("store_st", {"attr": "model_file"}),
                labels="Select your trained model:",
                file_type="model"
            )
        )

        self.file_selection_container.main_layout.addWidget(
            FileSelector(
                file_selected=("store_st", {"attr": "data_file"}),
                labels="Select data for classification:",
                file_type="npy"
            )
        )

        self.main_layout.addWidget(self.file_selection_container)

        self.main_layout.addStretch(1)

        self.main_layout.addWidget(CustomFooter((
            ("back to menu", "switch_modes", {"ui_cls": Menu}),
            ("start sorting", "start_sorting")
        )))

        self.subs: list = [
            ("store_st", self.store_user_input),
            ("start_sorting", self.start_sorting)
        ]

        for sub in self.subs:
            eb.subscribe(*sub)

    def store_user_input(self, value, meta) -> None:
        self.input_dict[meta["attr"]] = value

    def start_sorting(self) -> None:

        if all(key in self.input_dict for key in ("model_file", "data_file", "sort_dir")):

            if self.warning_state is True:
                self.main_layout.removeWidget(self.input_warning_text)
                self.input_warning_text.deleteLater()
                self.warning_state = False

            try:

                test_load = np.load(self.input_dict["model_file"], allow_pickle=True).item()
                dim = test_load["weights"][1].shape[0]
                test_load = np.load(self.input_dict["data_file"], allow_pickle=True).item()
                data_dim = test_load["input"].shape[1]
                assert dim == data_dim

            except (AssertionError, AttributeError, FileNotFoundError, OSError, IOError, pickle.UnpicklingError):

                self.input_warning_text = WarningText("Files failed to load!")
                last_index = self.main_layout.count()
                self.main_layout.insertWidget(last_index - 1, self.input_warning_text, alignment=qtc.Qt.AlignRight)
                self.warning_state = True
                raise
                return None

            new_manager = SortingManager()
            new_manager.update_params(self.input_dict)

            subscription = (f"sorting_crashed_{new_manager.id}", self.on_sorting_crash)
            self.subs.append(subscription)
            eb.subscribe(*subscription)

            new_manager.start_sorting()

            self.sorting_popup = LoadingWindow(
                f'''Sorting by the "{os.path.basename(self.input_dict["model_file"])}" model in progress''',
                f"sorting_done_{new_manager.id}",
                f"sorting_canceled_{new_manager.id}"
            )
            self.sorting_popup.setModal(False)
            self.sorting_popup.exec()

        elif self.warning_state is False:

            self.input_warning_text = WarningText("Mandatory input missing!")
            last_index = self.main_layout.count()
            self.main_layout.insertWidget(last_index - 1, self.input_warning_text, alignment=qtc.Qt.AlignRight)
            self.warning_state = True

        else:
            return None

    def cleanup(self) -> None:
        for sub in self.subs:
            eb.unsubscribe(sub[0], True)

    def on_sorting_crash(self, exception_info, manager_id):
        eb.emit(f"sorting_done_{manager_id}")
        eb.unsubscribe(f"sorting_crashed_{manager_id}", self.on_sorting_crash)

        exc_type = exception_info.get("type", "Exception")
        exc_msg = exception_info.get("message", "Unknown error")
        exc_traceback = exception_info.get("traceback", "")

        if exc_type in __builtins__:
            exc_class = __builtins__[exc_type]
        else:
            exc_class = Exception
        print(exc_traceback, file=sys.stderr)
        raise exc_class(exc_msg) from None