## var mn = Math.Log(maxTime/retryIn)/log(factor)

from math import log

def time(n, r, f): return r*(f**n)
def maxn(l, r, f): return log(l/r) / log(f)

for i in range(10):
	print(i, time(i, 1, 1.5))

print(maxn(39, 1, 1.5))