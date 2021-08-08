#!/usr/bin/python3
# -*- coding: UTF-8 -*-

import os
import sys
import random
import pandas
import keyboard
from mypinyin import Pinyin
from selenium import webdriver
from selenium.webdriver.support.ui import Select

driver = 0
p = Pinyin()
data = []


def fill_in():
    driver.switch_to.frame('parentDialog')
    # 姓名（文字）
    xm = driver.find_element_by_id('xm').get_attribute('value')
    # 银行名称
    yhdm = Select(driver.find_element_by_id('yhdm'))
    # 银行卡号
    yhkh = driver.find_element_by_id('yhkh')
    # 姓名拼音
    xmpy = driver.find_element_by_id('xmpy')
    # 曾用名
    cym = driver.find_element_by_id('cym')
    # 身高
    sg = driver.find_element_by_id('sg')
    # 体重
    tz = driver.find_element_by_id('tz')
    # 特长
    tc = driver.find_element_by_id('tc')
    # 健康状况
    jkzk = Select(driver.find_element_by_id('jkzk'))
    # 培养层次
    pycc = Select(driver.find_element_by_id('pycc'))
    # 是否走读生
    sfzd = Select(driver.find_element_by_id('sfzd'))
    # 考生类别
    kslb = Select(driver.find_element_by_id('kslb'))
    # 入学方式
    rxfs = Select(driver.find_element_by_id('rxfs'))
    # 培养方式
    pyfs = Select(driver.find_element_by_id('pyfs'))
    # 户籍性质
    zd5 = Select(driver.find_element_by_id('zd5'))
    # 宗教信仰
    zjdm = Select(driver.find_element_by_id('zjdm'))
    flag_found = False
    for item in data:
        if item[0].replace(' ', '') == xm:
            flag_found = True
            if '人民' in item[3]:
                yhdm.select_by_visible_text('中国人民银行')
            elif '建设' in item[3]:
                yhdm.select_by_visible_text('中国建设银行')
            elif ('邮政' or '邮储') in item[3]:
                yhdm.select_by_visible_text('中国邮政银行')
            elif '工商' in item[3]:
                yhdm.select_by_visible_text('中国工商银行')
            elif '中国银行' == item[3]:
                yhdm.select_by_visible_text('中国银行')
            elif '农业' in item[3]:
                yhdm.select_by_visible_text('中国农业银行')
            yhkh.clear()
            yhkh.send_keys(str(item[4]))
            xmpy.clear()
            xmpy.send_keys(p.get_pinyin(item[0].replace(' ', '')).split('-'))
            if item[1] != '无':
                cym.clear()
                cym.send_keys(item[1])
            sg.clear()
            sg.send_keys(str(random.randint(160, 180)))
            tz.clear()
            tz.send_keys(str(random.randint(50, 70)))
            tc.clear()
            tmp = random.randint(0, 9)
            if tmp == 0:
                tc.send_keys('钢琴')
            elif tmp == 1:
                tc.send_keys('吉他')
            elif tmp == 2:
                tc.send_keys('唱歌')
            elif tmp == 3:
                tc.send_keys('跳舞')
            elif tmp == 4:
                tc.send_keys('篮球')
            elif tmp == 5:
                tc.send_keys('足球')
            elif tmp == 6:
                tc.send_keys('书法')
            elif tmp == 7:
                tc.send_keys('绘画')
            elif tmp == 8:
                tc.send_keys('英语')
            else:
                tc.send_keys('计算机')
            jkzk.select_by_visible_text('健康状态')
            pycc.select_by_visible_text('普通培养')
            sfzd.select_by_visible_text('否')
            kslb.select_by_visible_text('二类')
            rxfs.select_by_visible_text('选校入学')
            pyfs.select_by_visible_text('统招')
            if item[2] == '农村':
                zd5.select_by_visible_text('农村')
            elif item[2] == '城市':
                zd5.select_by_visible_text('城市')
            elif item[2] == '县镇非农':
                zd5.select_by_visible_text('县镇非农')
            zjdm.select_by_visible_text('无宗教信仰')
            break
    driver.switch_to.default_content()
    if flag_found == False:
        msg = '在“data.xlsx”中未找到“'+xm+'”同学的信息！'
        print(msg)
        driver.execute_script('alert("'+msg.replace('\n', '\\n')+'");')


if __name__ == '__main__':
    print('本程序由环境 20-1 边宇琨编写，仅用于自动填充，不会收集任何信息。\n在开始之前，请开启 Windows 系统的扩展名显示，并将学生基本信息表按照“模板.xlsx”的格式进行整理，命名为“data.xlsx”，放置于程序目录下。\n这非常重要，如果信息表格式不正确或未按说明操作，程序可能无法正常运行！\n准备完成后，按下 Enter 键以加载信息表。')
    input()
    df = pandas.read_excel('data.xlsx')
    data = df.values
    driver = webdriver.Edge(os.path.split(
        os.path.realpath(sys.argv[0]))[0]+'\msedgedriver.exe')
    driver.get('http://10.10.8.15/xgxt')
    msg = '请仔细阅读以下说明！\n稍后，请登录并于在校生信息管理页面筛选出您负责的班级，在学生信息修改窗口中按下 Ctrl + Alt + F 热键，届时，程序将自动匹配并填充信息。\n重要提醒：请在保存之前人工检查信息！'
    print(msg)
    driver.execute_script('alert("'+msg.replace('\n', '\\n')+'");')
    keyboard.add_hotkey('Ctrl + Alt + F', fill_in)
    keyboard.wait()
