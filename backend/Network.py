# -*- coding: utf-8 -*-
"""
Created on Sun Aug 15 12:55:38 2021

@author: vavri
"""
import numpy as np
import time
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
import os
from glob import glob


class Network(object):

    path_ = os.path.join('..', '..', 'training_params')

    #activation functions

    @staticmethod
    def ReLU(x, d=False):

        return np.where(x < 0, 0., (x if d is False else 1))

    @staticmethod
    def Sigmoid(x, d=False):

        return (1/(1+np.exp(-x))) if d is False\
        else ((1/(1+np.exp(-x)))*(1-(1/(1+np.exp(-x)))))

    @staticmethod
    def extract_int(string, cut=None):

        if cut is None:
            n_str = ''
            for i, char in enumerate(string):
                n_str += char if char.isdigit() is True else ''
            n_str = int(n_str)

        else:
            try:
                assert cut in ['first', 'last'], "Split needs to be either \"first\" or \"last\""
            except AssertionError:
                raise

            if cut == "first":
                n_str = ''
                for i, char in enumerate(string):
                    n_str += char if char.isdigit() is True else ''
                    if char == '_': break

            if cut == "last":
                n_str = ''
                for i,char1 in enumerate(string):
                    if char1 == '_':
                        for char2 in string[i:]:
                            n_str += char2 if char2.isdigit() is True else ''
                        break
        return int(n_str)

    @staticmethod
    def current_index():

        path_ = str(np.copy(Network.path_))
        all_files = glob(os.path.join(path_,'p*.npy'))
        if all_files:
            end_basename = os.path.basename(all_files[-1])
            extracted = Network.extract_int(end_basename,cut='first')
            return extracted
        else: return 0

    @staticmethod
    def clear_dir(indices=None):

        try:
            path_ = str(np.copy(Network.path_))
            if indices is not None:

                files = []

                for n in indices:
                    n = int(n)
                    files_ = glob(os.path.join(path_,f'p{n}*.npy'))
                    files.extend(files_)

                for file in files:
                    os.remove(file)

            else:
                files = glob(os.path.join(path_,'p*.npy'))
                for file in files:
                    os.remove(file)

        except (ValueError, IndexError):
            print('Warning: Input indices must correspond with existing parameter files!')

    def __init__(self, index=None, skip_init=False):  # if index is left None, then the latest existing is used

        if skip_init: return

        bias_load = []
        weight_load = []
        path_ = str(np.copy(Network.path_))
        all_files = glob(os.path.join(path_,'*.npy'))

        try:
            assert len(all_files) != 0, "No parameter files available!"
        except AssertionError:
            raise  # TODO proper error handling

        last_file = os.path.basename(all_files[-1])
        last_index = Network.extract_int(last_file, cut='first')

        if index is not None:
            par_indices = []
            for file in all_files:
                bfile = os.path.basename(file)
                par_index = Network.extract_int(bfile, cut='first')
                par_indices.append(par_index)

            try:
                assert index in par_indices, "Listed parameters dont exist!"
            except AssertionError:
                raise  # TODO proper error handling

            p_ind = index

        else: p_ind = last_index

        w_par_files = glob(os.path.join(path_, f'p{p_ind}_w*.npy'))
        b_par_files = glob(os.path.join(path_, f'p{p_ind}_b*.npy'))

        for file in w_par_files:
            weight_load.append(np.load(file))

        for file in b_par_files:
            bias_load.append(np.load(file))

        params = [weight_load,bias_load]

        N = [len(weight_load[0][:,0])]
        for matrix in weight_load:
            N.append(len(matrix[0,:]))

        self.N = N

        weight_like = [0]*len(self.N)
        bias_like = [0]*len(self.N)

        for l in range(1, len(self.N)):
            weight_like[l] = np.zeros((self.N[l-1], self.N[l]))
            bias_like[l] = np.zeros((self.N[l]))

        self.weight_like = weight_like
        self.bias_like = bias_like
        self.weights = self.weight_like[:]
        self.bias = self.bias_like[:]

        for l in range(len(self.N)):
            self.weights[l] = params[0][l-1]
            self.bias[l] = params[1][l-1]

    # output method

    def get_output(self, inp_, layer=False, label=None):

        # first layer output

        p_output = np.copy(inp_)

        if layer is True:

            all_out_act = []
            all_out = []
            all_out_act.append(p_output[:])
            all_out.append(p_output[:])

        # rest of the layers propagating

        if (layer > 1) or (len(self.N) > 1):

            for l in range(1,(len(self.N) if type(layer) == bool else layer)):

                act_matrix = np.full((self.N[l],self.N[l-1]),p_output)
                act_matrix = np.transpose(act_matrix)*self.weights[l]
                activation = np.sum(act_matrix,axis=0) + self.bias[l]

                if l < (len(self.N)-1): p_output = Network.ReLU(activation)
                else: p_output = Network.Sigmoid(activation)

                if layer is True:

                    all_out_act.append(activation[:])
                    all_out.append(p_output[:])

        # computing cost

        if label is not None:

            if layer is False or isinstance(layer, int):
                output = np.copy(p_output)
            else:
                output = np.copy(all_out[-1])

            dif = label - output
            cost = np.sum(dif**2)
            self.cost = cost

        if layer is True:
            return [np.copy(all_out_act),np.copy(all_out)]
        else:
            return np.copy(p_output)

    def load_params(self, path):

        file = np.load(path, allow_pickle=True)
        self.weights = file["weights"]
        self.bias = file["bias"]
        self.N = [len(vec) for vec in self.bias]
        self.N.insert(0, self.weights[0].shape()[0])

        for l in range(1, len(self.N)):
            self.weight_like[l] = np.zeros((self.N[l-1], self.N[l]))
            self.bias_like[l] = np.zeros((self.N[l]))

