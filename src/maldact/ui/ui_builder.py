
import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
from maldact.ui.input_widgets import DefaultWidget


class Transitions:

    def __init__(self, window, active_ui):
        self.window = window
        if active_ui is None:
            self.active_ui = DefaultWidget()
            self.window.setCentralWidget(self.active_ui)
        else:
            self.active_ui = active_ui
        self.central_widget = self.window.centralWidget()
        self.stored_instances = {}
        self.old_widget = None

    def switch_ui(self, new_ui):
        self.central_widget = self.window.centralWidget()
        self.window.setCentralWidget(new_ui)
        self.central_widget.deleteLater()
        self.active_ui = new_ui

    def replace_widget(self, old_widget, new_widget, parent, stored=None):
        # Get the parent of the old widget
        parent_layout = parent
        self.old_widget = old_widget
        if old_widget is None:
            self.old_widget = self.stored_instances[stored]

        if parent_layout is not None:
            # Find the index of the old widget in the parent layout
            index = parent_layout.indexOf(self.old_widget)

            if index != -1:
                # Remove the old widget from the parent layout
                parent_layout.removeWidget(self.old_widget)
                self.old_widget.deleteLater()

                # Insert the new widget at the same index
                parent_layout.insertWidget(index, new_widget)
                if stored is not None:
                    self.stored_instances[stored] = new_widget

