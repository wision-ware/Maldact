import sys
import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import PyQt5.QtGui as qtg
from ui_manager import UIManager as um


class MainWindow(qtw.QMainWindow):

    def __init__(self):

        super().__init__()

        self.setWindowTitle("Maldact")
        self.resize(960, 540)

        um.initialize(self)


def main():

    app = qtw.QApplication(sys.argv)
    default_font = qtg.QFont("Arial", 10)

    app.setAttribute(qtc.Qt.AA_EnableHighDpiScaling)
    app.setFont(default_font)

    window = MainWindow()
    window.show()
    sys.exit(app.exec_())


if __name__ == "__main__":
    main()