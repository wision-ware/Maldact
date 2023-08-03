import sys
import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
from widgets import UiFileSelector, UiMModeButtons
from signal_manager import SignalManager as sm


class MainWindow(qtw.QMainWindow):

    def __init__(self):

        super().__init__()
        self.setWindowTitle("Maldact")

        # create and set up the UI
        self.ui = UiMModeButtons(self)
        self.setCentralWidget(self.ui)

def main():

    app = qtw.QApplication(sys.argv)
    window = MainWindow()
    window.show()

    sys.exit(app.exec_())

if __name__ == "__main__":
    main()